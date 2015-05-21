// <copyright file="Integration.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Tests.Sdk
{
    using Xunit;

    public static class Integration
    {
        public abstract class Database : IUseFixture<SqlDatabaseFixture>
        {
            public string ConnectionString { get; set; }

            public void SetFixture(SqlDatabaseFixture fixture)
            {
                this.ConnectionString = fixture.ConnectionString.Replace("=master;", string.Concat("=", fixture.DatabaseName, ";"));
            }
        }
    }
}
