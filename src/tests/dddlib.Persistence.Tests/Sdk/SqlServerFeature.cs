// <copyright file="SqlServerFeature.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Tests.Sdk
{
    using System.Data.SqlClient;
    using Microsoft.SqlServer.Management.Common;
    using Xunit;

    public abstract class SqlServerFeature : Feature, IUseFixture<SqlServerFixture>
    {
        // LINK (Cameron): https://github.com/xbehave/xbehave.net/wiki/Can%27t-access-fixture-data-when-using-IUseFixture
        private static readonly Integration.Database IntegrationDatabase = new Integration.Database();

        public string ConnectionString
        {
            get { return IntegrationDatabase.ConnectionString; }
        }

        public void SetFixture(SqlServerFixture fixture)
        {
            IntegrationDatabase.SetFixture(fixture);
        }

        protected void ExecuteSql(string sql)
        {
            using (var connection = new SqlConnection(this.ConnectionString))
            {
                var serverConnection = new ServerConnection(connection);

                serverConnection.ExecuteNonQuery(sql);
            }
        }
    }
}
