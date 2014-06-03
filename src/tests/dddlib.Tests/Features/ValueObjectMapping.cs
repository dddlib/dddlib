// <copyright file="ValueObjectMapping.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Features
{
    using dddlib.Tests.Sdk;

    public abstract class ValueObjectMapping : Feature
    {
        /*
            Value Object Equality
            ---------------------
            with equality comparer (undefined)
            with equality comparer (defined in bootstrapper)
            with equality comparer (defined in metadata)
            with equality comparer (defined in both bootstrapper and metadata - same)
            with equality comparer (defined in both bootstrapper and metadata - different)
            with equality comparer (defined in type: override) MAYBE NOT?
            with equality comparer (defined in type: override and other?) MAYBE NOT?
        */
    }
}
