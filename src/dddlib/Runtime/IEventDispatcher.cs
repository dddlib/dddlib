// <copyright file="IEventDispatcher.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    internal interface IEventDispatcher
    {
        void Dispatch(AggregateRoot aggregate, object @event);
    }
}
