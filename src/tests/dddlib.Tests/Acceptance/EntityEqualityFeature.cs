// <copyright file="EntityEqualityFeature.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Acceptance
{
    public class EntityEqualityFeature
    {
        /*
            Entity Equality
            ---------------
            with natural key selector (undefined)
            with natural key selector (defined in bootstrapper)
            with natural key selector (defined in metadata)
            with natural key selector (defined in both bootstrapper and metadata - same)
            with natural key selector (defined in both bootstrapper and metadata - different)

            Entity Equality (special case: string)
            --------------------------------------
            with natural key selector (defined - doesn't matter how) AND with natural key equality comparer (undefined)
            with natural key selector (defined - doesn't matter how) AND with natural key equality comparer (string only, defined in bootstrapper only)

            Entity Equality (special case: composite key value object: strings)
            -------------------------------------------------------------------
            with natural key selector (defined - doesn't matter how)

            Entity Equality (Inherited)
            ---------------------------
            with natural key selector (undefined)
            with natural key selector (undefined in base)
            with natural key selector (undefined in subclass)

            [all entity equality tests should also work for aggregate roots]
        */
    }
}
