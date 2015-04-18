// <copyright file="SqlServerNaturalKeyRepository.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.SqlServer
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Transactions;
    using dddlib.Persistence.Sdk;

    /// <summary>
    /// Represents the SQL Server natural key repository.
    /// </summary>
    public class SqlServerNaturalKeyRepository : INaturalKeyRepository
    {
        private readonly string connectionString;
        private readonly string schema;

        private bool isInitialized;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerNaturalKeyRepository"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="schema">The schema.</param>
        public SqlServerNaturalKeyRepository(string connectionString, string schema)
        {
            Guard.Against.InvalidConnectionString(() => connectionString);
            Guard.Against.NullOrEmpty(() => schema);

            this.connectionString = connectionString;
            this.schema = schema;

            // TODO (Cameron): Check the database version is valid.
        }

        /// <summary>
        /// Gets the natural key records for the specified type of aggregate root from the specified checkpoint.
        /// </summary>
        /// <param name="aggregateRootType">Type of the aggregate root.</param>
        /// <param name="checkpoint">The checkpoint.</param>
        /// <returns>The natural key records.</returns>
        public IEnumerable<NaturalKeyRecord> GetNaturalKeys(Type aggregateRootType, long checkpoint)
        {
            Guard.Against.Null(() => aggregateRootType);

            if (!this.isInitialized)
            {
                this.InitializeStorage();
            }

            using (new TransactionScope(TransactionScopeOption.Suppress))
            using (var connection = new SqlConnection(this.connectionString))
            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = string.Concat(this.schema, ".GetNaturalKeys");
                command.Parameters.Add("@AggregateRootTypeName", SqlDbType.VarChar, 511).Value = aggregateRootType.Name;
                command.Parameters.Add("@Checkpoint", SqlDbType.BigInt).Value = checkpoint;

                connection.Open();

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        yield return new NaturalKeyRecord
                        {
                            Identity = new Guid(Convert.ToString(reader["Identity"])),
                            SerializedValue = (string)reader["Value"],
                            Checkpoint = Convert.ToInt64(reader["Checkpoint"]),
                        };
                    }
                }
            }
        }

        /// <summary>
        /// Attempts to add the natural key to the natural key records.
        /// </summary>
        /// <param name="aggregateRootType">Type of the aggregate root.</param>
        /// <param name="serializedNaturalKey">The serialized natural key.</param>
        /// <param name="checkpoint">The checkpoint.</param>
        /// <param name="naturalKeyRecord">The natural key record.</param>
        /// <returns>Returns <c>true</c> if the natural key record was successfully added; otherwise <c>false</c>.</returns>
        public bool TryAddNaturalKey(Type aggregateRootType, object serializedNaturalKey, long checkpoint, out NaturalKeyRecord naturalKeyRecord)
        {
            Guard.Against.Null(() => aggregateRootType);
            Guard.Against.Null(() => serializedNaturalKey);

            if (!this.isInitialized)
            {
                this.InitializeStorage();
            }

            using (new TransactionScope(TransactionScopeOption.Suppress))
            using (var connection = new SqlConnection(this.connectionString))
            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = string.Concat(this.schema, ".TryAddNaturalKey");
                command.Parameters.Add("@AggregateRootTypeName", SqlDbType.VarChar, 511).Value = aggregateRootType.Name;
                command.Parameters.Add("@Value", SqlDbType.VarChar).Value = serializedNaturalKey;
                command.Parameters.Add("@Checkpoint", SqlDbType.BigInt).Value = checkpoint;

                connection.Open();

                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        naturalKeyRecord = null;
                        return false;
                    }

                    naturalKeyRecord = new NaturalKeyRecord
                    {
                        Identity = new Guid(Convert.ToString(reader["Identity"])),
                        SerializedValue = (string)serializedNaturalKey,
                        Checkpoint = Convert.ToInt64(reader["Checkpoint"]),
                    };

                    return true;
                }
            }
        }

        private void InitializeStorage()
        {
            ////using (new TransactionScope(TransactionScopeOption.Suppress))
            ////using (var connection = new SqlConnection(this.connectionString))
            ////using (var command = connection.CreateCommand())
            ////{
            ////    connection.Open();

            ////    command.CommandText = Storage.Version01.Replace("[dbo]", string.Concat("[", this.schema, "]"));
            ////    command.ExecuteNonQuery();
            ////}

            this.isInitialized = true;
        }
    }
}
