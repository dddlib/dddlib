// <copyright file="SqlServerEventStore.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Projections.SqlServer
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
    using Sdk;

    /// <summary>
    /// Represents the SQL Server event store (for the event dispatcher).
    /// </summary>
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

            Serializer.RegisterConverters(new[] { new DateTimeConverter() });
        }

        /// <summary>
        /// Gets the events from the specified sequence number.
        /// </summary>
        /// <param name="sequenceNumber">The sequence number.</param>
        /// <returns>The events.</returns>
        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1118:ParameterMustNotSpanMultipleLines", Justification = "It's fine here.")]
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

                        yield return @event;
                    }
                }
            }
        }
    }
}
