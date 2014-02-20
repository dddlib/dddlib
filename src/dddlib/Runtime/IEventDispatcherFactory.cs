// <copyright file="IEventDispatcherFactory.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;

    /// <summary>
    /// Exposes the public members of the event dispatcher factory.
    /// </summary>
    public interface IEventDispatcherFactory
    {
        /// <summary>
        /// Creates the event dispatcher.
        /// </summary>
        /// <param name="type">The type to create an event dispatcher for.</param>
        /// <returns>The event dispatcher.</returns>
        IEventDispatcher CreateEventDispatcher(Type type);
    }
}
