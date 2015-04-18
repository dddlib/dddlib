// <copyright file="SqlServerIdentityMapTests.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Tests.Storage
{
    using System;
    using dddlib.Persistence.SqlServer;
    using Xunit;

    public class SqlServerIdentityMapTests
    {
        [Fact(Skip = "Not ready.")]
        public void Test()
        {
            var connectionString = @"Data Source=(localdb)\ProjectsV12;Initial Catalog=dddlib.Persistence;Integrated Security=True;";
            var identityMap = new SqlServerIdentityMap(connectionString);

            var registrationA = new Registration("123");
            var registrationB = new Registration("abc");
            var registrationC = new Registration("ABC");

            var identity = default(Guid);
            var success = identityMap.TryGet(typeof(Car), registrationA, out identity);

            var identityA = identityMap.GetOrAdd(typeof(Car), registrationA);
            var identity1 = default(Guid);
            var success1 = identityMap.TryGet(typeof(Car), registrationA, out identity1);

            var identity2 = default(Guid);
            var success2 = identityMap.TryGet(typeof(Car), registrationA, out identity2);

            var identityA2 = identityMap.GetOrAdd(typeof(Car), registrationA);

            ////var identity3 = default(Guid);
            ////var success3 = identityMap.TryGet(typeof(Car), registrationA, out identity3);

            ////var identity4 = default(Guid);
            ////var success4 = identityMap.TryGet(typeof(Car), registrationA, out identity4);
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

        private class Car : AggregateRoot
        {
            public Registration Registration { get; private set; }
        }
    }
}
