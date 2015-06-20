// <copyright file="SqlServerIdentityMap.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.SqlServer
{
    using dddlib.Persistence.Sdk;

    /// <summary>
    /// Represents a SQL-based identity map.
    /// </summary>
    public class SqlServerIdentityMap : DefaultIdentityMap
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerIdentityMap"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        public SqlServerIdentityMap(string connectionString)
            : this(connectionString, "dbo")
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerIdentityMap"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="schema">The database schema.</param>
        public SqlServerIdentityMap(string connectionString, string schema)
            : base(new SqlServerNaturalKeyRepository(connectionString, schema), new DefaultNaturalKeySerializer())
        {
        }
    }
}
