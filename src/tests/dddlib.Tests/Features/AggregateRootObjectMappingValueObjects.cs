// <copyright file="AggregateRootObjectMappingValueObjects.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Features
{
    using dddlib.Tests.Sdk;

    // As someone who uses dddlib
    // In order to create events from domain objects passed to [command] methods [on an aggregate root]
    // I need to be able to map between value objects and DTO's (to and from)
    public abstract class AggregateRootObjectMappingValueObjects : Feature
    {
        /*
            So...
            You have an aggregate with a natural key value object
            You recreate the value object in the event handler
            The logic for the value object changes over time
            The re-creation fails upon reconstitution because the logic has changed
            The solution is... some sort of mapping...
         */
    }
}
