// <copyright file="SqlServerSnapshotStore.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.SqlServer
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Transactions;
    using System.Web.Script.Serialization;
    using dddlib.Persistence.Sdk;

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
        private readonly Guid partition;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerSnapshotStore"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        public SqlServerSnapshotStore(string connectionString)
            : this(connectionString, "dbo", Guid.Empty)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerSnapshotStore"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="schema">The schema.</param>
        public SqlServerSnapshotStore(string connectionString, string schema)
            : this(connectionString, schema, Guid.Empty)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerSnapshotStore"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="partition">The partition.</param>
        internal SqlServerSnapshotStore(string connectionString, Guid partition)
            : this(connectionString, "dbo", partition)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerSnapshotStore"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="schema">The schema.</param>
        /// <param name="partition">The partition.</param>
        internal SqlServerSnapshotStore(string connectionString, string schema, Guid partition)
        {
            Guard.Against.NullOrEmpty(() => schema);

            this.connectionString = connectionString;
            this.schema = schema;
            this.partition = partition;

            var connection = new SqlConnection(connectionString);
            connection.InitializeSchema(schema, "SqlServerPersistence");
            connection.InitializeSchema(schema, typeof(SqlServerSnapshotStore));
        }

        /// <summary>
        /// Gets the snapshot for a stream.
        /// </summary>
        /// <param name="streamId">The stream identifier.</param>
        /// <returns>The snapshot.</returns>
        public Snapshot GetSnapshot(Guid streamId)
        {
            // TODO (Cameron): Use partition for the call.
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
            // TODO (Cameron): Use partition for the call.
            using (new TransactionScope(TransactionScopeOption.Suppress))
            using (var connection = new SqlConnection(this.connectionString))
            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = string.Concat(this.schema, ".PutSnapshot");
                command.Parameters.Add("@StreamId", SqlDbType.UniqueIdentifier).Value = streamId;
                command.Parameters.Add("@StreamRevision", SqlDbType.Int).Value = snapshot.StreamRevision;
                command.Parameters.Add("@PayloadTypeName", SqlDbType.VarChar, 511).Value = 
                    string.Concat(snapshot.Memento.GetType().FullName, ", ", snapshot.Memento.GetType().Assembly.GetName().Name);
                command.Parameters.Add("@Payload", SqlDbType.VarChar).Value = Serializer.Serialize(snapshot.Memento);

                connection.Open();

                command.ExecuteNonQuery();
            }
        }
    }
}
