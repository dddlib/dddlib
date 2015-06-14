// <copyright file="Integration.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Tests.Sdk
{
    using Xunit;

    public static class Integration
    {
        public class Database : IClassFixture<SqlServerFixture>
        {
            public Database(SqlServerFixture fixture)
            {
                this.ConnectionString = fixture.ConnectionString.Replace("=master;", string.Concat("=", fixture.DatabaseName, ";"));
            }

            public string ConnectionString { get; set; }
        }
    }
}
