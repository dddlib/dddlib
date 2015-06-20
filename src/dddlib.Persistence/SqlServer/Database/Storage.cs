// <copyright file="Storage.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.SqlServer.Database
{
    using System;
    using System.Data.SqlClient;
    using System.Globalization;
    using System.Transactions;

    internal class Storage
    {
        private readonly string connectionString;
        private readonly Scripts scripts;

        public Storage(string connectionString, string schema)
        {
            Guard.Against.InvalidConnectionString(() => connectionString);
            Guard.Against.Null(() => schema);

            this.connectionString = connectionString;
            this.scripts = new Scripts(schema);
        }

        public void Initialize(int storageVersion)
        {
            Guard.Against.NegativeOrZero(() => storageVersion);

            using (var connection = new SqlConnection(this.connectionString))
            {
                connection.Open();

                var databaseVersion = this.GetDatabaseVersion(connection);
                if (databaseVersion == storageVersion)
                {
                    return;
                }

                if (databaseVersion > storageVersion)
                {
                    throw new PersistenceException(
                        string.Format(
                            CultureInfo.InvariantCulture, 
                            "The database version '{0}' is ahead of the storage version '{1}' supported by this library.", 
                            databaseVersion, 
                            storageVersion));
                }

                if (databaseVersion == 0)
                {
                    this.CreateSchema(connection);
                }

                for (var version = databaseVersion + 1; version <= storageVersion; version++)
                {
                    this.UpgradeDatabaseVersion(connection, version);
                }
            }
        }

        private int GetDatabaseVersion(SqlConnection connection)
        {
            using (var transaction = new TransactionScope(TransactionScopeOption.Suppress))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = this.scripts.GetDatabaseVersion;

                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        private void CreateSchema(SqlConnection connection)
        {
            using (var transaction = new TransactionScope(TransactionScopeOption.RequiresNew))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = this.scripts.CreateSchema;

                command.ExecuteNonQuery();
            }
        }

        private void UpgradeDatabaseVersion(SqlConnection connection, int version)
        {
            using (var transaction = new TransactionScope(TransactionScopeOption.RequiresNew))
            {
                foreach (var script in this.scripts.Database[version])
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = script;
                        command.ExecuteNonQuery();
                    }
                }

                transaction.Complete();
            }
        }
    }
}
