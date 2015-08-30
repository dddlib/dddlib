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

        private long currentEventId;
        private long currentBatchId;

        /// <summary>
        /// Occurs when an event is committed.
        /// </summary>
        public event EventHandler<EventCommittedEventArgs> OnEventCommitted;

        /// <summary>
        /// Occurs when a batch is prepared.
        /// </summary>
        public event EventHandler<BatchPrearedEventArgs> OnBatchPrepared;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerNotificationService"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        public SqlServerNotificationService(string connectionString)
        {
            this.connectionString = connectionString;

            SqlDependency.Start(connectionString);

            this.MonitorEvents();
            this.MonitorBatches();
        }

        private void MonitorEvents()
        {
            using (var connection = new SqlConnection(connectionString))
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
                        var eventId = Convert.ToInt64(reader["EventId"]);
                        if (this.currentEventId == eventId)
                        {
                            return;
                        }

                        this.currentEventId = eventId;

                        if (this.OnEventCommitted != null)
                        {
                            this.OnEventCommitted.Invoke(this, new EventCommittedEventArgs(this.currentEventId));
                        }
                    }
                }
            }
        }

        private void MonitorBatches()
        {
            using (var connection = new SqlConnection(connectionString))
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
                        var batchId = Convert.ToInt64(reader["BatchId"]);
                        if (this.currentBatchId == batchId)
                        {
                            return;
                        }

                        this.currentBatchId = batchId;

                        if (this.OnBatchPrepared != null)
                        {
                            this.OnBatchPrepared.Invoke(this, new BatchPrearedEventArgs(this.currentBatchId));
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

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            SqlDependency.Stop(connectionString);
        }
    }
}
