// <copyright file="IEventDispatcher.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    /*  TODO (Cameron): 
        Change to work on any type.  */

    /// <summary>
    /// Exposes the public members of the event dispatcher.
    /// </summary>
    public interface IEventDispatcher
    {
        /// <summary>
        /// Dispatches the specified event against the specified aggregate.
        /// </summary>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="event">The event.</param>
        void Dispatch(AggregateRoot aggregate, object @event);
    }
}
