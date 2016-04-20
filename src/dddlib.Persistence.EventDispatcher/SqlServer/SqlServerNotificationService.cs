// <copyright file="SqlServerNotificationService.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.EventDispatcher.SqlServer
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Transactions;
    using dddlib.Persistence.EventDispatcher.Sdk;

    /// <summary>
    /// Represents the SQL Server notification service.
    /// </summary>
    public class SqlServerNotificationService : INotificationService, IDisposable
    {
        private readonly string connectionString;
        private readonly string schema;
        private readonly Guid partition;
        private readonly string dispatcherId;

        private long currentSequenceNumber;
        private long currentBatchId;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerNotificationService"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        public SqlServerNotificationService(string connectionString)
            : this(connectionString, "dbo", Guid.Empty, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerNotificationService"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="schema">The schema.</param>
        public SqlServerNotificationService(string connectionString, string schema)
            : this(connectionString, schema, Guid.Empty, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerNotificationService"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="schema">The schema.</param>
        /// <param name="dispatcherId">The dispatcher identifier.</param>
        public SqlServerNotificationService(string connectionString, string schema, string dispatcherId)
            : this(connectionString, schema, Guid.Empty, dispatcherId)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerNotificationService"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="partition">The partition.</param>
        internal SqlServerNotificationService(string connectionString, Guid partition)
            : this(connectionString, "dbo", partition, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerNotificationService"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="schema">The schema.</param>
        /// <param name="partition">The partition.</param>
        internal SqlServerNotificationService(string connectionString, string schema, Guid partition)
            : this(connectionString, "dbo", partition, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerNotificationService" /> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="schema">The schema.</param>
        /// <param name="partition">The partition.</param>
        /// <param name="dispatcherId">The dispatcher identifier.</param>
        internal SqlServerNotificationService(string connectionString, string schema, Guid partition, string dispatcherId)
        {
            Guard.Against.NullOrEmpty(() => schema);

            if (dispatcherId != null && dispatcherId.Length > 10)
            {
                throw new ArgumentException("Dispatcher identity cannot be more than 10 character long.", Guard.Expression.Parse(() => dispatcherId));
            }

            this.connectionString = connectionString;
            this.schema = schema;
            this.partition = partition;
            this.dispatcherId = dispatcherId;

            var connection = new SqlConnection(connectionString);
            connection.InitializeSchema(schema, "SqlServerPersistence");
            connection.InitializeSchema(schema, typeof(SqlServerEventStore));
            connection.InitializeSchema(schema, typeof(SqlServerEventDispatcher));

            SqlDependency.Start(this.connectionString);

            this.MonitorEvents();
            this.MonitorBatches();
        }

        /// <summary>
        /// Occurs when an event is committed.
        /// </summary>
        public event EventHandler<EventCommittedEventArgs> OnEventCommitted;

        /// <summary>
        /// Occurs when a batch is prepared.
        /// </summary>
        public event EventHandler<BatchPreparedEventArgs> OnBatchPrepared;

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            SqlDependency.Stop(this.connectionString);
        }

        private void MonitorEvents()
        {
            using (new TransactionScope(TransactionScopeOption.Suppress))
            using (var connection = new SqlConnection(this.connectionString))
            using (var command = new SqlCommand(string.Concat(this.schema, ".MonitorUndispatchedEvents"), connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add("SequenceNumber", SqlDbType.VarChar).Value = this.currentSequenceNumber;
                command.Notification = null;

                connection.Open();

                var sqlDependency = new SqlDependency(command);
                sqlDependency.OnChange += this.SqlDependencyOnEventComitted;

                // TODO (Cameron): Try execute scalar.
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        var sequenceNumber = Convert.ToInt64(reader["SequenceNumber"]);
                        if (this.currentSequenceNumber == sequenceNumber)
                        {
                            return;
                        }

                        this.currentSequenceNumber = sequenceNumber;

                        if (this.OnEventCommitted != null)
                        {
                            this.OnEventCommitted.Invoke(this, new EventCommittedEventArgs(this.currentSequenceNumber));
                        }
                    }
                }
            }
        }

        private void MonitorBatches()
        {
            using (new TransactionScope(TransactionScopeOption.Suppress))
            using (var connection = new SqlConnection(this.connectionString))
            using (var command = new SqlCommand(string.Concat(this.schema, ".MonitorUndispatchedBatches"), connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add("DispatcherId", SqlDbType.VarChar).Value = this.dispatcherId == null ? (object)DBNull.Value : this.dispatcherId;
                command.Notification = null;

                connection.Open();

                var sqlDependency = new SqlDependency(command);
                sqlDependency.OnChange += this.SqlDependencyOnBatchPrepared;

                // TODO (Cameron): Try execute scalar.
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        var batchId = Convert.ToInt64(reader["Id"]);
                        if (this.currentBatchId == batchId)
                        {
                            return;
                        }

                        this.currentBatchId = batchId;

                        if (this.OnBatchPrepared != null)
                        {
                            this.OnBatchPrepared.Invoke(this, new BatchPreparedEventArgs(this.currentBatchId));
                        }
                    }
                }
            }
        }

        private void SqlDependencyOnEventComitted(object sender, SqlNotificationEventArgs eventArgs)
        {
            var sqlDependency = (SqlDependency)sender;
            sqlDependency.OnChange -= this.SqlDependencyOnEventComitted;

            if (eventArgs.Info == SqlNotificationInfo.Invalid)
            {
                // TODO (Cameron): Fix exception type.
                throw new Exception("Unable to process.");
            }

            this.MonitorEvents();
        }

        private void SqlDependencyOnBatchPrepared(object sender, SqlNotificationEventArgs eventArgs)
        {
            var sqlDependency = (SqlDependency)sender;
            sqlDependency.OnChange -= this.SqlDependencyOnBatchPrepared;

            if (eventArgs.Info == SqlNotificationInfo.Invalid)
            {
                // TODO (Cameron): Fix exception type.
                throw new Exception("Unable to process.");
            }

            this.MonitorBatches();
        }
    }
}
