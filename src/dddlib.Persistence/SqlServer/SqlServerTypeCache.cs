// <copyright file="SqlServerTypeCache.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.SqlServer
{
    using System;
    using System.Collections.Concurrent;
    using System.Data;
    using System.Data.SqlClient;
    using System.Diagnostics.CodeAnalysis;
    using System.Transactions;
    using dddlib.Persistence.Sdk;
    using dddlib.Sdk;

    /// <summary>
    /// Represents the SQL Server type cache.
    /// </summary>
    public sealed class SqlServerTypeCache : ITypeCache
    {
        private static readonly ConcurrentDictionary<string, Type> ResolvedTypes = new ConcurrentDictionary<string, Type>();

        private readonly ConcurrentDictionary<int, Type> types = new ConcurrentDictionary<int, Type>();
        private readonly ConcurrentDictionary<Type, int> typeIds = new ConcurrentDictionary<Type, int>();
        private readonly ConcurrentDictionary<int, string> typeNames = new ConcurrentDictionary<int, string>();

        private readonly string connectionString;
        private readonly string schema;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerTypeCache"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        public SqlServerTypeCache(string connectionString)
            : this(connectionString, "dbo")
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerTypeCache"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="schema">The schema.</param>
        public SqlServerTypeCache(string connectionString, string schema)
        {
            Guard.Against.NullOrEmpty(() => schema);

            this.connectionString = connectionString;
            this.schema = schema;

            var connection = new SqlConnection(connectionString);
            connection.InitializeSchema(schema, typeof(SqlServerTypeCache));
        }

        /// <summary>
        /// Gets the type for the specified type identifier.
        /// </summary>
        /// <param name="typeId">The type identifier.</param>
        /// <returns>The type.</returns>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1632:DocumentationTextMustMeetMinimumCharacterLength", Justification = "Reviewed.")]
        public Type GetType(int typeId)
        {
            Guard.Against.NegativeOrZero(() => typeId);

            var type = default(Type);
            if (!this.types.TryGetValue(typeId, out type))
            {
                this.Synchronize();
                this.types.TryGetValue(typeId, out type);
            }

            return type;
        }

        /// <summary>
        /// Gets the type name.
        /// </summary>
        /// <param name="typeId">The type identifier.</param>
        /// <returns>The type name.</returns>
        public string GetTypeName(int typeId)
        {
            Guard.Against.NegativeOrZero(() => typeId);

            var typeName = default(string);
            if (!this.typeNames.TryGetValue(typeId, out typeName))
            {
                this.Synchronize();
                this.typeNames.TryGetValue(typeId, out typeName);
            }

            return typeName;
        }

        /// <summary>
        /// Gets the type identifier for the specified type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>The type identifier.</returns>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1632:DocumentationTextMustMeetMinimumCharacterLength", Justification = "Reviewed.")]
        public int GetTypeId(Type type)
        {
            Guard.Against.Null(() => type);

            var typeId = default(int);
            if (this.typeIds.TryGetValue(type, out typeId))
            {
                return typeId;
            }

            // NOTE (Cameron): This type may have already been added concurrently but this is an unlikely scenario and won't break anything.
            this.TryAddType(type);
            this.Synchronize();
            this.typeIds.TryGetValue(type, out typeId);

            return typeId;
        }

        private void TryAddType(Type type)
        {
            using (new TransactionScope(TransactionScopeOption.Suppress))
            using (var connection = new SqlConnection(this.connectionString))
            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = string.Concat(this.schema, ".TryAddType");
                command.Parameters.Add("@Name", SqlDbType.VarChar, 511).Value = type.GetSerializedName();

                connection.Open();

                command.ExecuteNonQuery();
            }
        }

        private void Synchronize()
        {
            using (new TransactionScope(TransactionScopeOption.Suppress))
            using (var connection = new SqlConnection(this.connectionString))
            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = string.Concat(this.schema, ".GetTypes");

                connection.Open();

                using (var reader = command.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        var typeId = reader.GetInt32(0 /* Id */);
                        if (this.types.ContainsKey(typeId))
                        {
                            continue;
                        }

                        var typeName = reader.GetString(1 /* Name */);
                        var type = Type.GetType(typeName);

                        this.types.TryAdd(typeId, type);
                        this.typeNames.TryAdd(typeId, typeName);

                        if (type != null)
                        {
                            this.typeIds.TryAdd(type, typeId);
                        }
                    }
                }
            }
        }
    }
}
