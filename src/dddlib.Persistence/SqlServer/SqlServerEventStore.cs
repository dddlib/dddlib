// <copyright file="SqlServerEventStore.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.SqlServer
{
    using System;
    using System.Collections.Generic;
    using dddlib.Persistence.Sdk;

    // TODO (Cameron): Make public. Implement.
    internal class SqlServerEventStore : IEventStore
    {
        private readonly string connectionString;
        private readonly string schema;

        public SqlServerEventStore(string connectionString)
            : this(connectionString, "dbo")
        {
        }

        public SqlServerEventStore(string connectionString, string schema)
        {
            Guard.Against.Null(() => connectionString);
            Guard.Against.Null(() => schema);

            this.connectionString = connectionString;
            this.schema = schema;
        }

        public void CommitStream(Guid streamId, IEnumerable<object> events, string preCommitState, out string postCommitState)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<object> GetStream(Guid streamId, int streamRevision, out string state)
        {
            throw new NotImplementedException();
        }

        public void AddSnapshot(Guid streamId, Snapshot snapshot)
        {
            throw new NotImplementedException();
        }

        public Snapshot GetSnapshot(Guid streamId)
        {
            throw new NotImplementedException();
        }
    }
}
