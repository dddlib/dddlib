// <copyright file="SqlServerEventStore.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.SqlServer
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Transactions;
    using System.Web.Script.Serialization;
    using dddlib.Persistence.Sdk;
    using dddlib.Sdk;

    /// <summary>
    /// Represents the SQL Server event store.
    /// </summary>
    /// <seealso cref="dddlib.Persistence.Sdk.IEventStore" />
    public class SqlServerEventStore : IEventStore
    {
        // NOTE (Cameron): This is nonsense and should be moved out of here.
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

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
        /// Gets the events for a stream.
        /// </summary>
        /// <param name="streamId">The stream identifier.</param>
        /// <param name="streamRevision">The stream revision to get the events from.</param>
        /// <param name="state">The state of the steam.</param>
        /// <returns>The events.</returns>
        public IEnumerable<object> GetStream(Guid streamId, int streamRevision, out string state)
        {
            state = null;

            // TODO (Cameron): Use partition for the call.
            using (new TransactionScope(TransactionScopeOption.Suppress))
            using (var connection = new SqlConnection(this.connectionString))
            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = string.Concat(this.schema, ".GetStream");
                command.Parameters.Add("@StreamId", SqlDbType.UniqueIdentifier).Value = streamId;
                command.Parameters.Add("@StreamRevision", SqlDbType.Int).Value = streamRevision;

                connection.Open();

                using (var reader = command.ExecuteReader())
                {
                    var events = new List<object>();

                    // TODO (Cameron): This is massively inefficient.
                    while (reader.Read())
                    {
                        var payloadTypeName = Convert.ToString(reader["PayloadTypeName"]);
                        var payloadType = Type.GetType(payloadTypeName);
                        var @event = Serializer.Deserialize(Convert.ToString(reader["Payload"]), payloadType);

                        events.Add(@event);

                        state = Convert.ToString(reader["State"]);
                   }

                    return events;
                }
            }
        }

        /// <summary>
        /// Commits the events to a stream.
        /// </summary>
        /// <param name="streamId">The stream identifier.</param>
        /// <param name="events">The events to commit.</param>
        /// <param name="correlationId">The correlation identifier.</param>
        /// <param name="preCommitState">The pre-commit state of the stream.</param>
        /// <param name="postCommitState">The post-commit state of stream.</param>
        public void CommitStream(Guid streamId, IEnumerable<object> events, Guid correlationId, string preCommitState, out string postCommitState)
        {
            var data = new DataTable();
            data.Columns.Add("Index").DataType = typeof(int);
            data.Columns.Add("PayloadTypeName").DataType = typeof(string);
            data.Columns.Add("Payload").DataType = typeof(string);

            var index = 0;
            foreach (var @event in events)
            {
                data.Rows.Add(++index, @event.GetType().GetSerializedName(), Serializer.Serialize(@event));
            }

            // add all events into temporary table
            // TODO (Cameron): Use partition for the call.
            // TODO (Cameron): Use partition for the call.
            using (new TransactionScope(TransactionScopeOption.Suppress))
            using (var connection = new SqlConnection(this.connectionString))
            {
                connection.Open();

                using (var command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = @"CREATE TABLE #Events
(
    [Index] INT NOT NULL,
    [PayloadTypeName] VARCHAR(511) NOT NULL,
    [Payload] VARCHAR(MAX) NOT NULL
)";

                    command.ExecuteNonQuery();
                }

                using (var bulkCopy = new SqlBulkCopy(connection))
                {
                    // TODO (Cameron): Async.
                    bulkCopy.DestinationTableName = "#Events";
                    bulkCopy.WriteToServer(data);
                }

                using (var command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandText = string.Concat(this.schema, ".CommitStream");
                    command.Parameters.Add("@StreamId", SqlDbType.UniqueIdentifier).Value = streamId;
                    command.Parameters.Add("@CorrelationId", SqlDbType.UniqueIdentifier).Value = correlationId;
                    command.Parameters.Add("@PreCommitState", SqlDbType.VarChar, 36).Value = (object)preCommitState ?? DBNull.Value;
                    command.Parameters.Add("@PostCommitState", SqlDbType.VarChar, 36).Direction = ParameterDirection.Output;

                    try
                    {
                        command.ExecuteNonQuery();
                    }
                    catch (SqlException ex)
                    {
                        if (ex.Errors.Cast<SqlError>().Any(error => error.Number != 50000))
                        {
                            throw;
                        }

                        throw new ConcurrencyException(ex.Message, ex);
                    }

                    postCommitState = Convert.ToString(command.Parameters["@PostCommitState"].Value);
                }
            }
        }
    }
}
