// <copyright file="CarRepository.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace perftest.SqlServer
{
    using System;
    using dddlib.Persistence.SqlServer;

    public class CarRepository : dddlib.Persistence.Sdk.Repository<Car>
    {
        public CarRepository(string connectionString)
            : base(new SqlServerIdentityMap(connectionString))
        {
        }

        protected override object Load(Guid id, out string state)
        {
            throw new NotImplementedException();
        }

        protected override void Save(Guid id, object memento, string preCommitState, out string postCommitState)
        {
            throw new NotImplementedException();
        }
    }
}
