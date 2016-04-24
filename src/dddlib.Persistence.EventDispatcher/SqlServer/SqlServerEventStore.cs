// <copyright file="SqlServerEventStore.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.EventDispatcher.SqlServer
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;
    using System.Transactions;
    using System.Web.Script.Serialization;
    using dddlib.Persistence.EventDispatcher.Sdk;

    /// <summary>
    /// Represents the SQL Server event store (for the event dispatcher).
    /// </summary>
    public class SqlServerEventStore : IEventStore
    {
        // NOTE (Cameron): This is nonsense and should be moved out of here.
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        private readonly string connectionString;
        private readonly string schema;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerEventStore"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        public SqlServerEventStore(string connectionString)
            : this(connectionString, "dbo")
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerEventStore"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="schema">The schema.</param>
        public SqlServerEventStore(string connectionString, string schema)
        {
            Guard.Against.NullOrEmpty(() => schema);

            this.connectionString = connectionString;
            this.schema = schema;

            var connection = new SqlConnection(connectionString);
            connection.InitializeSchema(schema, "SqlServerPersistence");
            connection.InitializeSchema(schema, typeof(SqlServerEventStore));
            connection.InitializeSchema(schema, typeof(SqlServerEventDispatcher));

            Serializer.RegisterConverters(new[] { new DateTimeConverter() });
        }

        /// <summary>
        /// Gets the next undispatched events batch.
        /// </summary>
        /// <param name="dispatcherId">The dispatcher identifier.</param>
        /// <param name="batchSize">Size of the batch.</param>
        /// <returns>The events batch.</returns>
        public Batch GetNextUndispatchedEventsBatch(Guid dispatcherId, int batchSize)
        {
            using (new TransactionScope(TransactionScopeOption.Suppress))
            using (var connection = new SqlConnection(this.connectionString))
            using (var command = new SqlCommand(string.Concat(this.schema, ".GetNextUndispatchedEventsBatch"), connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add("DispatcherId", SqlDbType.UniqueIdentifier).Value = dispatcherId;
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

                    // TODO (Cameron): This is massively inefficient.
                    while (reader.Read())
                    {
                        var payloadTypeName = Convert.ToString(reader["PayloadTypeName"]);
                        var payloadType = Type.GetType(payloadTypeName);
                        var payload = Serializer.Deserialize(Convert.ToString(reader["Payload"]), payloadType);

                        events.Add(
                            new Event
                            {
                                Id = Convert.ToInt64(reader["SequenceNumber"]),
                                Payload = payload,
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
        /// <param name="dispatcherId">The dispatcher identifier.</param>
        /// <param name="sequenceNumber">The sequence number for the event.</param>
        public void MarkEventAsDispatched(Guid dispatcherId, long sequenceNumber)
        {
            using (new TransactionScope(TransactionScopeOption.Suppress))
            using (var connection = new SqlConnection(this.connectionString))
            using (var command = new SqlCommand(string.Concat(this.schema, ".MarkEventAsDispatched"), connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add("DispatcherId", SqlDbType.UniqueIdentifier).Value = dispatcherId;
                command.Parameters.Add("SequenceNumber", SqlDbType.Int).Value = sequenceNumber;

                connection.Open();

                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Gets the events from the specified sequence number.
        /// </summary>
        /// <param name="sequenceNumber">The sequence number.</param>
        /// <returns>The events.</returns>
        public IEnumerable<object> GetEventsFrom(long sequenceNumber)
        {
            using (new TransactionScope(TransactionScopeOption.Suppress))
            using (var connection = new SqlConnection(this.connectionString))
            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = string.Concat(this.schema, ".GetEventsFrom");
                command.Parameters.Add("@SequenceNumber", SqlDbType.Int).Value = sequenceNumber;

                connection.Open();

                using (var reader = command.ExecuteReader())
                {
                    // TODO (Cameron): This is massively inefficient.
                    while (reader.Read())
                    {
                        var payloadTypeName = Convert.ToString(reader["PayloadTypeName"]);
                        var payloadType = Type.GetType(payloadTypeName);
                        var @event = Serializer.Deserialize(Convert.ToString(reader["Payload"]), payloadType);

                        yield return @event;
                    }
                }
            }
        }
    }
}
