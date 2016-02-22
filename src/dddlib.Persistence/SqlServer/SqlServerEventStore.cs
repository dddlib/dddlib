// <copyright file="SqlServerEventStore.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.SqlServer
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using dddlib.Persistence.Sdk;

    // TODO (Cameron): Make public. Implement.
    internal class SqlServerEventStore : IEventStore
    {
        private readonly string connectionString;
        private readonly string schema;
        private readonly Guid partition;

        public SqlServerEventStore(string connectionString)
            : this(connectionString, "dbo", Guid.Empty)
        {
        }

        public SqlServerEventStore(string connectionString, string schema)
            : this(connectionString, schema, Guid.Empty)
        {
        }

        public SqlServerEventStore(string connectionString, Guid partition)
            : this(connectionString, "dbo", partition)
        {
        }

        public SqlServerEventStore(string connectionString, string schema, Guid partition)
        {
            new SqlConnection(connectionString).InitializeSchema(schema, typeof(SqlServerEventStore));

            this.connectionString = connectionString;
            this.schema = schema;
            this.partition = partition;
        }

        public void CommitStream(Guid streamId, IEnumerable<object> events, Guid commitId, string preCommitState, out string postCommitState)
        {
            // add all events into temporary table
            // TODO (Cameron): Use partition for the call.
            throw new NotImplementedException();
        }

        public IEnumerable<object> GetStream(Guid streamId, int streamRevision, out string state)
        {
            // TODO (Cameron): Use partition for the call.
            throw new NotImplementedException();
        }
    }
}
