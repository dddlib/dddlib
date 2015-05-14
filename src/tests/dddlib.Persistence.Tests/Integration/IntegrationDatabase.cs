// <copyright file="IntegrationDatabase.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Tests.Integration
{
    using System;
    using System.Configuration;
    using System.Data.SqlClient;
    using Microsoft.SqlServer.Management.Common;
    using Microsoft.SqlServer.Management.Smo;

    public class IntegrationDatabase : IDisposable
    {
        private readonly string databaseName = Guid.NewGuid().ToString("N");

        public IntegrationDatabase()
        {
            using (var connection = new SqlConnection(ConfigurationManager.ConnectionStrings["TestDB"].ConnectionString))
            {
                var serverConnection = new ServerConnection(connection);
                var server = new Server(serverConnection);
                var database = new Database(server, this.databaseName);

                database.Create();
            }
        }

        public string Name
        {
            get { return this.databaseName; }
        }

        public void Dispose()
        {
            using (var connection = new SqlConnection(ConfigurationManager.ConnectionStrings["TestDB"].ConnectionString))
            {
                var serverConnection = new ServerConnection(connection);
                var server = new Server(serverConnection);

                server.KillDatabase(this.databaseName);
            }
        }
    }
}
