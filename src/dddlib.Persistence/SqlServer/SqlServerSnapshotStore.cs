// <copyright file="SqlServerSnapshotStore.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.SqlServer
{
    using System;
    using dddlib.Persistence.Sdk;

    // TODO (Cameron): Make public. Implement.
    internal sealed class SqlServerSnapshotStore : ISnapshotStore
    {
        private readonly string connectionString;
        private readonly string schema;

        public SqlServerSnapshotStore(string connectionString)
            : this(connectionString, "dbo")
        {
        }

        public SqlServerSnapshotStore(string connectionString, string schema)
        {
            Guard.Against.Null(() => connectionString);
            Guard.Against.Null(() => schema);

            this.connectionString = connectionString;
            this.schema = schema;
        }

        public Snapshot GetSnapshot(Guid streamId)
        {
            throw new NotImplementedException();
        }

        public void PutSnapshot(Guid streamId, Snapshot snapshot)
        {
            throw new NotImplementedException();
        }
    }
}
