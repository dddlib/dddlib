// <copyright file="StorageTests.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Tests.Integration
{
    using dddlib.Persistence.Tests.Sdk;

    public class StorageTests : Integration.Database
    {
        public StorageTests(SqlServerFixture fixture)
            : base(fixture)
        {
        }

        /*  TODO (Cameron):
            Can upgrade to version 2, 3, 4... etc.
            Cannot create storage when storage tables already exist (include transactional rollback)
            Cannot upgrade when existing storage is the wrong version (include transactional rollback)
        */
    }
}
