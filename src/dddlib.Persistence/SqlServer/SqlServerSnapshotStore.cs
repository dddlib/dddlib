// <copyright file="SqlServerSnapshotStore.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.SqlServer
{
    using System;
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
    /// Represents the SQL Server snapshot store.
    /// </summary>
    /// <seealso cref="dddlib.Persistence.Sdk.ISnapshotStore" />
    public sealed class SqlServerSnapshotStore : ISnapshotStore
    {
        // NOTE (Cameron): This is nonsense and should be moved out of here.
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        private readonly string connectionString;
        private readonly string schema;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerSnapshotStore"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        public SqlServerSnapshotStore(string connectionString)
            : this(connectionString, "dbo")
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerSnapshotStore"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="schema">The schema.</param>
        public SqlServerSnapshotStore(string connectionString, string schema)
        {
            Guard.Against.NullOrEmpty(() => schema);

            this.connectionString = connectionString;
            this.schema = schema;

            var connection = new SqlConnection(connectionString);
            connection.InitializeSchema(schema, "SqlServerPersistence");
            connection.InitializeSchema(schema, typeof(SqlServerSnapshotStore));

            Serializer.RegisterConverters(new[] { new DateTimeConverter() });
        }

        /// <summary>
        /// Gets the snapshot for a stream.
        /// </summary>
        /// <param name="streamId">The stream identifier.</param>
        /// <returns>The snapshot.</returns>
        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1118:ParameterMustNotSpanMultipleLines", Justification = "It's fine here.")]
        public Snapshot GetSnapshot(Guid streamId)
        {
            using (new TransactionScope(TransactionScopeOption.Suppress))
            using (var connection = new SqlConnection(this.connectionString))
            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = string.Concat(this.schema, ".GetSnapshot");
                command.Parameters.Add("@StreamId", SqlDbType.UniqueIdentifier).Value = streamId;

                connection.Open();

                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return null;
                    }

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

                    return new Snapshot
                    {
                        StreamRevision = Convert.ToInt32(reader["StreamRevision"]),
                        Memento = Serializer.Deserialize(Convert.ToString(reader["Payload"]), payloadType),
                    };
                }
            }
        }

        /// <summary>
        /// Adds or updates the snapshot for a stream.
        /// </summary>
        /// <param name="streamId">The stream identifier.</param>
        /// <param name="snapshot">The snapshot.</param>
        public void PutSnapshot(Guid streamId, Snapshot snapshot)
        {
            Guard.Against.Null(() => snapshot);
            Guard.Against.Null(() => snapshot.Memento);

            using (new TransactionScope(TransactionScopeOption.Suppress))
            using (var connection = new SqlConnection(this.connectionString))
            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = string.Concat(this.schema, ".PutSnapshot");
                command.Parameters.Add("@StreamId", SqlDbType.UniqueIdentifier).Value = streamId;
                command.Parameters.Add("@StreamRevision", SqlDbType.Int).Value = snapshot.StreamRevision;
                command.Parameters.Add("@PayloadTypeName", SqlDbType.VarChar, 511).Value = snapshot.Memento.GetType().GetSerializedName();
                command.Parameters.Add("@Payload", SqlDbType.VarChar).Value = Serializer.Serialize(snapshot.Memento);

                connection.Open();

                command.ExecuteNonQuery();
            }
        }
    }
}
