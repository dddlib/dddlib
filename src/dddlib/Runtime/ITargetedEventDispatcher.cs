// <copyright file="ITargetedEventDispatcher.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System.Diagnostics.CodeAnalysis;

    internal interface ITargetedEventDispatcher
    {
        [SuppressMessage("Microsoft.Naming", "CA1716:IdentifiersShouldNotMatchKeywords", MessageId = "event", Justification = "It is an event.")]
        void Dispatch(object target, object @event);
    }
}
