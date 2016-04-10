// <copyright file="SqlServerEventStore.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.EventDispatcher.SqlServer
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;
    using dddlib.Persistence.EventDispatcher.Sdk;

    /// <summary>
    /// Represents the SQL Server event store (for the event dispatcher).
    /// </summary>
    public class SqlServerEventStore : IEventStore
    {
        private readonly string connectionString;
        private readonly string schema;
        private readonly Guid partition;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerEventStore"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        public SqlServerEventStore(string connectionString)
            : this(connectionString, "dbo", Guid.Empty)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerEventStore"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="schema">The schema.</param>
        public SqlServerEventStore(string connectionString, string schema)
            : this(connectionString, schema, Guid.Empty)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerEventStore"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="partition">The partition.</param>
        internal SqlServerEventStore(string connectionString, Guid partition)
            : this(connectionString, "dbo", partition)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerEventStore"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="schema">The schema.</param>
        /// <param name="partition">The partition.</param>
        internal SqlServerEventStore(string connectionString, string schema, Guid partition)
        {
            Guard.Against.NullOrEmpty(() => schema);

            this.connectionString = connectionString;
            this.schema = schema;
            this.partition = partition;

            var connection = new SqlConnection(connectionString);
            connection.InitializeSchema(schema, "SqlServerPersistence");
            connection.InitializeSchema(schema, typeof(SqlServerEventStore));
        }

        /// <summary>
        /// Gets the next undispatched events batch.
        /// </summary>
        /// <param name="batchSize">Size of the batch.</param>
        /// <returns>The events batch.</returns>
        public Batch GetNextUndispatchedEventsBatch(int batchSize)
        {
            using (var connection = new SqlConnection(this.connectionString))
            using (var command = new SqlCommand("dbo.GetNextUndispatchedEventsBatch", connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add("MaxBatchSize", SqlDbType.Int).Value = batchSize;

                connection.Open();

                using (var reader = command.ExecuteReader())
                {
                    if (!reader.HasRows)
                    {
                        return null;
                    }

                    var batch = new Batch();
                    var events = new List<Event>();

                    while (reader.Read())
                    {
                        batch.Id = Convert.ToInt64(reader["BatchId"]);
                    }

                    reader.NextResult();

                    while (reader.Read())
                    {
                        events.Add(
                            new Event
                            {
                                Id = Convert.ToInt64(reader["EventId"]),
                                Payload = reader["Payload"].ToString(),
                            });
                    }

                    batch.Events = events.ToArray();

                    return batch;
                }
            }
        }

        /// <summary>
        /// Marks the event as dispatched.
        /// </summary>
        /// <param name="eventId">The event identifier.</param>
        public void MarkEventAsDispatched(long eventId)
        {
            using (var connection = new SqlConnection(this.connectionString))
            using (var command = new SqlCommand("dbo.MarkEventAsDispatched", connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add("EventId", SqlDbType.Int).Value = eventId;

                connection.Open();

                command.ExecuteNonQuery();
            }
        }
    }
}
