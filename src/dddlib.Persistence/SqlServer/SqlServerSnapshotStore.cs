// <copyright file="SqlServerSnapshotStore.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.SqlServer
{
    using System;
    using System.Data.SqlClient;
    using dddlib.Persistence.Sdk;

    // TODO (Cameron): Make public. Implement.
    internal sealed class SqlServerSnapshotStore : ISnapshotStore
    {
        private readonly string connectionString;
        private readonly string schema;
        private readonly Guid partition;

        public SqlServerSnapshotStore(string connectionString)
            : this(connectionString, "dbo", Guid.Empty)
        {
        }

        public SqlServerSnapshotStore(string connectionString, string schema)
            : this(connectionString, schema, Guid.Empty)
        {
        }

        public SqlServerSnapshotStore(string connectionString, Guid partition)
            : this(connectionString, "dbo", partition)
        {
        }

        public SqlServerSnapshotStore(string connectionString, string schema, Guid partition)
        {
            new SqlConnection(connectionString).InitializeSchema(schema, typeof(SqlServerSnapshotStore));

            this.connectionString = connectionString;
            this.schema = schema;
            this.partition = partition;
        }

        public Snapshot GetSnapshot(Guid streamId)
        {
            // TODO (Cameron): Use partition for the call.
            throw new NotImplementedException();
        }

        public void PutSnapshot(Guid streamId, Snapshot snapshot)
        {
            // TODO (Cameron): Use partition for the call.
            throw new NotImplementedException();
        }
    }
}
