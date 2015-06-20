// <copyright file="UpgradeDatabaseVersionTests.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Tests.Integration
{
    using System;
    using System.Data.SqlClient;
    using System.IO;
    using dddlib.Persistence.SqlServer;
    using dddlib.Persistence.Tests.Sdk;
    using FluentAssertions;
    using Microsoft.SqlServer.Management.Common;
    using Xunit;

    // TODO (Cameron): Move to StorageTests.cs.
    public class UpgradeDatabaseVersionTests : Integration.Database
    {
        public UpgradeDatabaseVersionTests(SqlServerFixture fixture)
            : base(fixture)
        {
        }

        [Fact(Skip = "Not ready.")]
        public void CanUpdateToVersion2Functionality()
        {
            // NOTE (Cameron): Version #2 adds type forwarding.
            // arrange
            // set up version #1
            // add data to version #1
            var sqlScript = GetScript("dddlib.Persistence.SqlServer.Database.Scripts.Version01.sql");
            var naturalKey = Guid.NewGuid().ToString("N");
            this.ExecuteScript(sqlScript); // version #1
            this.ExecuteScript(@"exec [dbo].[TryAddNaturalKey] 'dddlib.Persistence.Tests.Integration.UpgradeDatabaseVersionTests+Vehicle', '{""Number"":""123""}', 0");

            // latest version
            var identityMap = new SqlServerIdentityMap(this.ConnectionString);
            var registration = new Registration(Guid.NewGuid().ToString("N"));

            // act
            var actualIdentity = identityMap.GetOrAdd(typeof(Vehicle), typeof(Registration), registration);

            // assert
            ////actualIdentity.Should().Be(expectedIdentity);
            // "dddlib.Persistence.Tests.Integration.SqlServerIdentityMapTests+Car"
        }

        private static string GetScript(string fullname)
        {
            using (var stream = typeof(SqlServerIdentityMap).Assembly.GetManifestResourceStream(fullname))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        private void ExecuteScript(string sqlScript)
        {
            using (var connection = new SqlConnection(this.ConnectionString))
            {
                new ServerConnection(connection).ExecuteNonQuery(sqlScript);
            }
        }

        private class Registration : ValueObject<Registration>
        {
            public Registration(string number)
            {
                Guard.Against.Null(() => number);

                this.Number = number;
            }

            public string Number { get; private set; }
        }

        private class Vehicle : AggregateRoot
        {
            public Registration Registration { get; private set; }
        }
    }
}
