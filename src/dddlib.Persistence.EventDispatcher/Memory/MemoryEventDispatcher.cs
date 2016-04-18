// <copyright file="MemoryEventDispatcher.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.EventDispatcher.Memory
{
    using System;
    using dddlib.Persistence.EventDispatcher.Sdk;

    // TODO (Cameron): This is going to be interesting...
    internal class MemoryEventDispatcher : EventDispatcher
    {
        private readonly IEventDispatcher eventDispatcher;

        public MemoryEventDispatcher(Action<long, object> eventDispatcherDelegate)
            : this(new CustomEventDispatcher(eventDispatcherDelegate))
        {
        }

        public MemoryEventDispatcher(IEventDispatcher eventDispatcher)
            : base(eventDispatcher, new MemoryEventStore(), null, 50)
        {
            Guard.Against.Null(() => eventDispatcher);

            this.eventDispatcher = eventDispatcher;
        }
    }
}
