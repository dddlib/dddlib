// <copyright file="SqlServerEventStore.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.SqlServer
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;
    using System.Runtime.Serialization;
    using System.Text;
    using System.Transactions;
    using System.Web.Script.Serialization;
    using dddlib.Persistence.Sdk;
    using dddlib.Sdk;
    using Microsoft.SqlServer.Server;

    /// <summary>
    /// Represents the SQL Server event store.
    /// </summary>
    /// <seealso cref="dddlib.Persistence.Sdk.IEventStore" />
    public class SqlServerEventStore : IEventStore
    {
        // NOTE (Cameron): This is nonsense and should be moved out of here.
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        // PERF (Cameron): Introduced to reduce memory allocations.
        private static readonly string Hostname = Environment.MachineName;

        // PERF (Cameron): Introduced to reduce allocations of the stored procedure names on each call.
        private readonly string getStreamStoredProcName;
        private readonly string commitStreamStoredProcName;
        private readonly ITypeCache typeCache;
        private readonly string connectionString;
        private readonly string schema;

        static SqlServerEventStore()
        {
            Serializer.RegisterConverters(new[] { new DateTimeConverter() });
        }

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

            this.getStreamStoredProcName = string.Concat(this.schema, ".GetStream");
            this.commitStreamStoredProcName = string.Concat(this.schema, ".CommitStream");

            this.typeCache = new SqlServerTypeCache(connectionString, schema);
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
                command.CommandText = this.getStreamStoredProcName;
                command.Parameters.Add("@StreamId", SqlDbType.UniqueIdentifier).Value = streamId;
                command.Parameters.Add("@StreamRevision", SqlDbType.Int).Value = streamRevision;
                command.Parameters.Add("@State", SqlDbType.VarChar, 36).Direction = ParameterDirection.Output;

                connection.Open();

                try
                {
                    var events = new List<object>();

                    using (var reader = command.ExecuteReader(CommandBehavior.SingleResult))
                    {
                        while (reader.Read())
                        {
                            var typeId = reader.GetInt32(1 /* TypeId */);
                            var payloadType = this.typeCache.GetType(typeId);

                            if (payloadType == null)
                            {
                                var payloadTypeName = this.typeCache.GetTypeName(typeId);

                                throw new SerializationException(
                                    string.Format(
                                        CultureInfo.InvariantCulture,
                                        @"Cannot deserialize event into type of '{0}' as that type does not exist in the assembly '{1}' or the assembly is not referenced by the project.
To fix this issue:
- ensure that the assembly '{1}' contains the type '{0}', and
- check that the assembly '{1}' is referenced by the project.
Further information: https://github.com/dddlib/dddlib/wiki/Serialization",
                                        payloadTypeName.Split(',').FirstOrDefault(),
                                        payloadTypeName.Split(',').LastOrDefault()));
                            }

                            events.Add(Serializer.Deserialize(reader.GetString(2 /* Payload */), payloadType));
                        }
                    }

                    state = Convert.ToString(command.Parameters["@State"].Value);

                    return events;
                }
                catch (SqlException ex)
                {
                    // NOTE (Cameron): 500 Internal Server Error
                    if (ex.Errors.Cast<SqlError>().Any(sqlError => sqlError.Number == 50500))
                    {
                        throw new ConcurrencyException(ex.Message, ex);
                    }

                    throw;
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

            var metadata = new Metadata
            {
                Hostname = Hostname,
                Timestamp = DateTime.UtcNow
            };

            using (new TransactionScope(TransactionScopeOption.Suppress))
            using (var connection = new SqlConnection(this.connectionString))
            {
                connection.Open();

                using (var command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandText = this.commitStreamStoredProcName;
                    command.Parameters.Add("@StreamId", SqlDbType.UniqueIdentifier).Value = streamId;
                    command.Parameters.Add("@Events", SqlDbType.Structured).Value = new SqlEvents(this.typeCache, events, metadata);
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

        private class SqlEvents : IEnumerable<SqlDataRecord>
        {
            // NOTE (Cameron): This is nonsense and should be moved out of here.
            private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

            private static readonly SqlMetaData[] ColumnMetadata = new[]
            {
                new SqlMetaData("Index", SqlDbType.Int),
                new SqlMetaData("TypeId", SqlDbType.Int),
                new SqlMetaData("Metadata", SqlDbType.VarChar, -1),
                new SqlMetaData("Payload", SqlDbType.VarChar, -1),
            };

            private readonly SqlDataRecord record = new SqlDataRecord(ColumnMetadata);
            private readonly ITypeCache typeCache;
            private readonly IEnumerable<object> events;
            private readonly string metadata;

            static SqlEvents()
            {
                Serializer.RegisterConverters(new[] { new DateTimeConverter() });
            }

            public SqlEvents(ITypeCache typeCache, IEnumerable<object> events, object metadata)
            {
                this.typeCache = typeCache;
                this.events = events;
                this.metadata = Serializer.Serialize(metadata);
            }

            public IEnumerator<SqlDataRecord> GetEnumerator()
            {
                var index = 0;
                foreach (var @event in this.events)
                {
                    this.record.SetInt32(0, ++index);
                    this.record.SetInt32(1, this.typeCache.GetTypeId(@event.GetType()));
                    this.record.SetString(2, this.metadata);
                    this.record.SetString(3, Serializer.Serialize(@event));
                    yield return this.record;
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }
        }

        private class Metadata
        {
            public string Hostname { get; set; }

            public DateTime Timestamp { get; set; }
        }
    }
}
