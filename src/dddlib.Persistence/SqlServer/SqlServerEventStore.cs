// <copyright file="SqlServerEventStore.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.SqlServer
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;
    using System.Runtime.Serialization;
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

            Serializer.RegisterConverters(new[] { new DateTimeConverter() });
        }

        /// <summary>
        /// Gets the events for a stream.
        /// </summary>
        /// <param name="streamId">The stream identifier.</param>
        /// <param name="streamRevision">The stream revision to get the events from.</param>
        /// <param name="state">The state of the steam.</param>
        /// <returns>The events.</returns>
        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1118:ParameterMustNotSpanMultipleLines", Justification = "It's fine here.")]
        public IEnumerable<object> GetStream(Guid streamId, int streamRevision, out string state)
        {
            state = null;

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
                        if (payloadType == null)
                        {
                            throw new SerializationException(
                                string.Format(
                                    CultureInfo.InvariantCulture,
                                    @"Cannot deserialize event into type of '{0}' as that type does not exist in the assembly '{1}' or the assembly is not referenced by the project.
To fix this issue:
- ensure that the assembly '{1}' contains the type '{0}', and
- check that the the assembly '{1}' is referenced by the project.
Further information: https://github.com/dddlib/dddlib/wiki/Serialization",
                                    payloadTypeName.Split(',').FirstOrDefault(),
                                    payloadTypeName.Split(',').LastOrDefault()));
                        }

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
            Guard.Against.Null(() => events);

            var data = new DataTable();
            data.Columns.Add("Index").DataType = typeof(int);
            data.Columns.Add("PayloadTypeName").DataType = typeof(string);
            data.Columns.Add("Metadata").DataType = typeof(string);
            data.Columns.Add("Payload").DataType = typeof(string);

            // TODO (Cameron): Eliminate the overhead of custom serialization for metadata.
            var metadata = new Metadata
            {
                Hostname = Environment.MachineName,
                Timestamp = DateTime.UtcNow
            };

            var index = 0;
            foreach (var @event in events)
            {
                data.Rows.Add(
                    ++index,
                    @event.GetType().GetSerializedName(),
                    Serializer.Serialize(metadata),
                    Serializer.Serialize(@event));
            }

            // NOTE (Cameron): Add all events into temporary table.
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
    [Metadata] VARCHAR(MAX) NOT NULL,
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
                        // NOTE (Cameron): 500 Internal Server Error
                        if (ex.Errors.Cast<SqlError>().Any(sqlError => sqlError.Number == 50500))
                        {
                            throw new ConcurrencyException(ex.Message, ex);
                        }

                        // NOTE (Cameron): 409 Conflict
                        if (ex.Errors.Cast<SqlError>().Any(sqlError => sqlError.Number == 50409))
                        {
                            if (preCommitState == null)
                            {
                                throw new ConcurrencyException("Aggregate root already exists.", ex);
                            }
                            else
                            {
                                throw new ConcurrencyException(ex.Message, ex);
                            }
                        }

                        throw;
                    }

                    postCommitState = Convert.ToString(command.Parameters["@PostCommitState"].Value);
                }
            }
        }

        private class Metadata
        {
            public string Hostname { get; set; }

            public DateTime Timestamp { get; set; }
        }
    }
}
