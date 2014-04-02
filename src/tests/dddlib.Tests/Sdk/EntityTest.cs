// <copyright file="EntityTest.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Sdk
{
    using dddlib.Runtime;

    internal abstract class EntityTest<T> : Test
        where T : Entity
    {
        protected abstract void AssertValid(EntityType entityType);
    }
}
