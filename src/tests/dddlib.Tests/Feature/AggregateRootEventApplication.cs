// <copyright file="AggregateRootEventApplication.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Feature
{
    using dddlib.Tests.Sdk;

    // As someone who uses dddlib [with event sourcing]
    // In order to persist events
    // I need to be able to record changes in state
    public abstract class AggregateRootEventApplication : Feature
    {
        /*
            Can change name of aggregate apply method
            Can inherit from assembly where aggregate apply method is different
        */
    }
}
