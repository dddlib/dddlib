// <copyright file="MemoryEventDispatcher.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.EventDispatcher.Memory
{
    using System;
    using dddlib.Persistence.EventDispatcher;

    // TODO (Cameron): This is going to be interesting...
    internal class MemoryEventDispatcher
    {
        private readonly IEventDispatcher eventDispatcher;

        public MemoryEventDispatcher(Action<long, object> eventDispatcherDelegate)
            : this(new Wrapper(eventDispatcherDelegate))
        {
        }

        public MemoryEventDispatcher(IEventDispatcher eventDispatcher)
        {
            Guard.Against.Null(() => eventDispatcher);

            this.eventDispatcher = eventDispatcher;
        }

        private class Wrapper : IEventDispatcher
        {
            private readonly Action<long, object> eventDispatcherDelegate;

            public Wrapper(Action<long, object> eventDispatcherDelegate)
            {
                Guard.Against.Null(() => eventDispatcherDelegate);

                this.eventDispatcherDelegate = eventDispatcherDelegate;
            }

            public void Dispatch(long eventId, object @event)
            {
                this.eventDispatcherDelegate.Invoke(eventId, @event);
            }
        }
    }
}
