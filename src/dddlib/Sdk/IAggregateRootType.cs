// <copyright file="IAggregateRootType.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk
{
    using System;

    internal interface IAggregateRootType
    {
        Delegate UninitializedFactory { get; }

        IEventDispatcher EventDispatcher { get; }

        AggregateRootType.RuntimeOptions Options { get; }
    }
}
