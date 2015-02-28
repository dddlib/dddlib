// <copyright file="IEventDispatcher.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk
{
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Exposes the public members of the event dispatcher.
    /// </summary>
    public interface IEventDispatcher
    {
        /// <summary>
        /// Dispatches an event to the specified target object.
        /// </summary>
        /// <param name="target">The target object.</param>
        /// <param name="event">The event.</param>
        [SuppressMessage("Microsoft.Naming", "CA1716:IdentifiersShouldNotMatchKeywords", MessageId = "event", Justification = "It is an event.")]
        void Dispatch(object target, object @event);
    }
}
