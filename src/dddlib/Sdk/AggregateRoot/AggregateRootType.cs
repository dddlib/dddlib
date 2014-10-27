// <copyright file="AggregateRootType.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Globalization;

    internal class AggregateRootType
    {
        public AggregateRootType(Type runtimeType, Func<object> uninitializedFactory, ITargetedEventDispatcher eventDispatcher)
        {
            Guard.Against.Null(() => runtimeType);

            if (!typeof(AggregateRoot).IsAssignableFrom(runtimeType))
            {
                throw new RuntimeException(
                    string.Format(CultureInfo.InvariantCulture, "The specified runtime type '{0}' is not an aggregate root.", runtimeType));
            }

            if (uninitializedFactory != null && uninitializedFactory.Method.ReturnType != runtimeType)
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture, 
                        "The specified uninitialized factory return type '{0}' does not match the specified runtime type '{1}'.", 
                        uninitializedFactory.Method.ReturnType,
                        runtimeType));
            }

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
