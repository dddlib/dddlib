// <copyright file="DefaultEventDispatcherFactory.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;

    /// <summary>
    /// Represents the default event dispatcher factory.
    /// </summary>
    public class DefaultEventDispatcherFactory : IEventDispatcherFactory
    {
        /// <summary>
        /// Creates the event dispatcher.
        /// </summary>
        /// <param name="type">The type to create an event dispatcher for.</param>
        /// <returns>The event dispatcher.</returns>
        public IEventDispatcher CreateEventDispatcher(Type type)
        {
            return new DefaultEventDispatcher(type);
        }
    }
}
