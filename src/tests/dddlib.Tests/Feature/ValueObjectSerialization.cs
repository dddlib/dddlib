// <copyright file="ValueObjectSerialization.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Feature
{
    using dddlib.Tests.Sdk;

    // As someone who uses dddlib
    // In order to persist an aggregate root with a value object natural key that has a non-standard constructor
    // I need to be able to serialize and deserialize that value object (for the identity map)
    public abstract class ValueObjectSerialization : Feature
    {
    }
}
