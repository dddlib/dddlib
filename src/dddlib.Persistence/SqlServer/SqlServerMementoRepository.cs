// <copyright file="SqlServerMementoRepository.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.SqlServer
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;
    using System.Runtime.Serialization;
    using System.Transactions;
    using System.Web.Script.Serialization;
    using dddlib.Persistence.Sdk;
    using dddlib.Sdk;

    /// <summary>
    /// Represents the SQL Server memento repository.
    /// </summary>
    /// <typeparam name="T">The type of repository.</typeparam>
    /// <seealso cref="dddlib.Persistence.Sdk.Repository{T}" />
    public sealed class SqlServerMementoRepository<T> : Repository<T> where T : AggregateRoot
    {
        // NOTE (Cameron): This is nonsense and should be moved out of here.
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        private readonly string loadMementoStoredProcName;
        private readonly string saveMementoStoredProcName;
        private readonly ITypeCache typeCache;

        private readonly string connectionString;
        private readonly string schema;

        static SqlServerMementoRepository()
        {
            Serializer.RegisterConverters(new[] { new DateTimeConverter() });
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerMementoRepository{T}"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        public SqlServerMementoRepository(string connectionString)
            : this(connectionString, "dbo")
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerMementoRepository{T}"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="schema">The schema.</param>
        public SqlServerMementoRepository(string connectionString, string schema)
            : base(new SqlServerIdentityMap(connectionString, schema))
        {
            this.connectionString = connectionString;
            this.schema = schema;

            var connection = new SqlConnection(connectionString);
            connection.InitializeSchema(schema, "SqlServerPersistence");
            connection.InitializeSchema(schema, "SqlServerMementoRepository");

            this.loadMementoStoredProcName = string.Concat(this.schema, ".LoadMemento");
            this.saveMementoStoredProcName = string.Concat(this.schema, ".SaveMemento");

            this.typeCache = new SqlServerTypeCache(connectionString, schema);
        }

        /// <summary>
        /// Loads the memento and the state for the specified identifier.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="state">The state.</param>
        /// <returns>The memento.</returns>
        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1118:ParameterMustNotSpanMultipleLines", Justification = "It's fine here.")]
        protected override object Load(Guid id, out string state)
        {
            state = null;

            using (new TransactionScope(TransactionScopeOption.Suppress))
            using (var connection = new SqlConnection(this.connectionString))
            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = this.loadMementoStoredProcName;
                command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;

                connection.Open();

                object memento = null;

                try
                {
                    using (var reader = command.ExecuteReader(CommandBehavior.SingleResult))
                    {
                        while (reader.Read())
                        {
                            var typeId = reader.GetInt32(1 /* TypeId */);
                            var payloadType = this.typeCache.GetType(typeId);

                            if (payloadType == null)
                            {
                                var payloadTypeName = this.typeCache.GetTypeName(typeId);

                                throw new SerializationException(
                                    string.Format(
                                        CultureInfo.InvariantCulture,
                                        @"Cannot deserialize event into type of '{0}' as that type does not exist in the assembly '{1}' or the assembly is not referenced by the project.
To fix this issue:
- ensure that the assembly '{1}' contains the type '{0}', and
- check that the assembly '{1}' is referenced by the project.
Further information: https://github.com/dddlib/dddlib/wiki/Serialization",
                                        payloadTypeName.Split(',').FirstOrDefault(),
                                        payloadTypeName.Split(',').LastOrDefault()));
                            }

                            memento = Serializer.Deserialize(reader.GetString(2 /* Payload */), payloadType);
                            state = reader.GetString(3 /* State */);
                        }
                    }
                }
                catch (SqlException ex)
                {
                    // NOTE (Cameron): 500 Internal Server Error
                    if (ex.Errors.Cast<SqlError>().Any(sqlError => sqlError.Number == 50500))
                    {
                        throw new ConcurrencyException(ex.Message, ex);
                    }

                    throw;
                }

                return memento;
            }
        }

        /// <summary>
        /// Saves the memento for the specified identifier providing the state is valid.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="memento">The memento.</param>
        /// <param name="preCommitState">The pre-commit state of the memento.</param>
        /// <param name="postCommitState">The post-commit state of memento.</param>
        protected override void Save(Guid id, object memento, string preCommitState, out string postCommitState)
        {
            Guard.Against.Null(() => memento);

            using (new TransactionScope(TransactionScopeOption.Suppress))
            using (var connection = new SqlConnection(this.connectionString))
            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = this.saveMementoStoredProcName;
                command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
                command.Parameters.Add("@TypeId", SqlDbType.VarChar, 511).Value = this.typeCache.GetTypeId(memento.GetType());
                command.Parameters.Add("@Payload", SqlDbType.VarChar, -1).Value = Serializer.Serialize(memento);
                command.Parameters.Add("@PreCommitState", SqlDbType.VarChar, 36).Value = (object)preCommitState ?? DBNull.Value;
                command.Parameters.Add("@PostCommitState", SqlDbType.VarChar, 36).Direction = ParameterDirection.Output;
                connection.Open();

                try
                {
                    command.ExecuteNonQuery();
                }
                catch (SqlException ex)
                {
                    // NOTE (Cameron): 500 Internal Server Error
                    if (ex.Errors.Cast<SqlError>().Any(sqlError => sqlError.Number == 50500))
                    {
                        throw new ConcurrencyException(ex.Message, ex);
                    }

                    // NOTE (Cameron): 409 Conflict
                    if (ex.Errors.Cast<SqlError>().Any(sqlError => sqlError.Number == 50409))
                    {
                        if (preCommitState == null)
                        {
                            throw new ConcurrencyException("Aggregate root already exists.", ex);
                        }
                        else
                        {
                            throw new ConcurrencyException(ex.Message, ex);
                        }
                    }

                    throw;
                }

                postCommitState = Convert.ToString(command.Parameters["@PostCommitState"].Value);
            }
        }
    }
}
