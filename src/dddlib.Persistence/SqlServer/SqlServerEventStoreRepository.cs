// <copyright file="SqlServerEventStoreRepository.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.SqlServer
{
    using Sdk;

    /// <summary>
    /// Represents a SQL Server-based event store repository.
    /// </summary>
    /// <seealso cref="dddlib.Persistence.Sdk.EventStoreRepository" />
    public sealed class SqlServerEventStoreRepository : EventStoreRepository
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerEventStoreRepository"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        public SqlServerEventStoreRepository(string connectionString)
            : base(
                new SqlServerIdentityMap(connectionString),
                new SqlServerEventStore(connectionString),
                new SqlServerSnapshotStore(connectionString))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerEventStoreRepository"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="schema">The schema.</param>
        public SqlServerEventStoreRepository(string connectionString, string schema)
            : base(
                new SqlServerIdentityMap(connectionString, schema), 
                new SqlServerEventStore(connectionString, schema), 
                new SqlServerSnapshotStore(connectionString, schema))
        {
        }
    }
}
