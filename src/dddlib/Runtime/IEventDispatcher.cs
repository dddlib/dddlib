// <copyright file="IEventDispatcher.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Exposes the public members of the event dispatcher.
    /// </summary>
    public interface IEventDispatcher
    {
        /// <summary>
        /// Dispatches the specified event against the specified target.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="event">The event.</param>
        [SuppressMessage("Microsoft.Naming", "CA1716:IdentifiersShouldNotMatchKeywords", MessageId = "event", Justification = "It is an event.")]
        void Dispatch(object target, object @event);
    }
}
