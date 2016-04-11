// <copyright file="SqlServerNotificationService.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.EventDispatcher.SqlServer
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using dddlib.Persistence.EventDispatcher.Sdk;

    /// <summary>
    /// Represents the SQL Server notification service.
    /// </summary>
    public class SqlServerNotificationService : INotificationService, IDisposable
    {
        private readonly string connectionString;

        private long currentSequenceNumber;
        private long currentBatchId;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerNotificationService"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        public SqlServerNotificationService(string connectionString)
        {
            // TODO (Cameron): Implement the storage solution to catch invalid connection string and database setup.
            this.connectionString = connectionString;

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
            using (var connection = new SqlConnection(this.connectionString))
            using (var command = new SqlCommand("dbo.MonitorUndispatchedEvents", connection))
            {
                command.CommandType = CommandType.StoredProcedure;
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
            using (var connection = new SqlConnection(this.connectionString))
            using (var command = new SqlCommand("dbo.MonitorUndispatchedBatches", connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Notification = null;

                connection.Open();

                var sqlDependency = new SqlDependency(command);
                sqlDependency.OnChange += this.SqlDependencyOnBatchPrepared;

                // TODO (Cameron): Try execute scalar.
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        var batchId = Convert.ToInt64(reader["SequenceNumber"]);
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
            var dependency = (SqlDependency)sender;
            dependency.OnChange -= this.SqlDependencyOnEventComitted;

            if (eventArgs.Info == SqlNotificationInfo.Invalid)
            {
                throw new Exception("Unable to process.");
            }

            this.MonitorEvents();
        }

        private void SqlDependencyOnBatchPrepared(object sender, SqlNotificationEventArgs eventArgs)
        {
            var dependency = (SqlDependency)sender;
            dependency.OnChange -= this.SqlDependencyOnBatchPrepared;

            if (eventArgs.Info == SqlNotificationInfo.Invalid)
            {
                throw new Exception("Unable to process.");
            }

            this.MonitorBatches();
        }
    }
}
