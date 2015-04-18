// <copyright file="Version01.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Tests.Storage
{
    using System.Linq;
    using dddlib.Persistence.SqlServer;
    using Xunit;

    public class Version01
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
