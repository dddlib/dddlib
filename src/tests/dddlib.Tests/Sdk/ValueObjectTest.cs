// <copyright file="ValueObjectTest.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Sdk
{
    using dddlib.Runtime;

    internal abstract class ValueObjectTest<T> : Test
        where T : ValueObject<T>
    {
        protected abstract void AssertValid(ValueObjectType valueObjectType);
    }
}
