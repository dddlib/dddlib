// <copyright file="AggregateRootTest.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Sdk
{
    using dddlib.Runtime;

    internal abstract class AggregateRootTest<T> : EntityTest<T>
        where T : AggregateRoot
    {
        protected abstract void AssertValid(AggregateRootType aggregateRootType);
    }
}
