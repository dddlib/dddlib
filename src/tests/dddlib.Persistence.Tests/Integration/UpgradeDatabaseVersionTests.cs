// <copyright file="UpgradeDatabaseVersionTests.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Tests.Integration
{
    using System.Linq;
    using dddlib.Persistence.SqlServer;
    using Xunit;

    public class UpgradeDatabaseVersionTests
    {
        [Fact(Skip = "Not ready.")]
        public void CanInitializeStorage()
        {
            var connectionString = @"Data Source=(localdb)\ProjectsV12;Initial Catalog=dddlib.Persistence;Integrated Security=True;";
            var repository = new SqlServerNaturalKeyRepository(connectionString, "dbo");

            var naturalKeyRecords = repository.GetNaturalKeys(this.GetType(), 0).ToArray();
        }
    }
}
