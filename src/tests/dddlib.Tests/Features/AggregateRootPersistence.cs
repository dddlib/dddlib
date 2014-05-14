// <copyright file="AggregateRootPersistence.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Features
{
    using dddlib.Tests.Sdk;

    public abstract class AggregateRootPersistence : Feature
    {
        /*
            AggregateRoot Persistence (Guid)
            --------------------------------
            with natural key selector (undefined) AND with uninitialized factory (undefined)
            with natural key selector (defined - doesn't matter how) AND with uninitialized factory (undefined)
            with natural key selector (undefined) AND with uninitialized factory (defined in bootstrapper only)
            with natural key selector (defined - doesn't matter how) AND with uninitialized factory (defined in bootstrapper only)

            AggregateRoot Persistence (special case: string)
            ------------------------------------------------
            ALL FOLLOWING TEST: with natural key selector (defined - doesn't matter how) AND with uninitialized factory (defined in bootstrapper only)
            with natural key equality comparer (undefined)
            with natural key equality comparer (string only, defined in bootstrapper only)

            AggregateRoot Persistence (special case: composite key value object: strings)
            -----------------------------------------------------------------------------
            ALL FOLLOWING TEST: with natural key selector (defined - doesn't matter how) AND with uninitialized factory (defined in bootstrapper only)
            with natural key serializer (undefined)
            with natural key serializer (defined in bootstrapper)
            AND MORE?
        */
    }
}
