// <copyright file="AggregateRootType.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;

    internal class AggregateRootType
    {
        public AggregateRootType(Func<object> uninitializedFactory, ITargetedEventDispatcher eventDispatcher)
        {
            this.UninitializedFactory = uninitializedFactory;
            this.EventDispatcher = eventDispatcher;

            // NOTE (Cameron): Only persist events if there is a way to reconstitute the persisted object.
            var persistEvents = uninitializedFactory != null;

            // NOTE (Cameron): Only dispatch events IF there is an event dispatcher AND if there are any handler methods?
            // TODO (Cameron): Check to see if there are any handler methods.
            var dispatchEvents = eventDispatcher != null;

            this.Options = new RuntimeOptions(persistEvents, dispatchEvents);
        }

        public Func<object> UninitializedFactory { get; private set; }

        public ITargetedEventDispatcher EventDispatcher { get; private set; }

        public RuntimeOptions Options { get; private set; }

        public class RuntimeOptions
        {
            public RuntimeOptions(bool persistEvents, bool dispatchEvents)
            {
                this.PersistEvents = persistEvents;
                this.DispatchEvents = dispatchEvents;
            }

            public bool PersistEvents { get; private set; }

            public bool DispatchEvents { get; private set; }
        }
    }
}
