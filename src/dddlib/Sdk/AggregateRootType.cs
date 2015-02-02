// <copyright file="AggregateRootType.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk
{
    using System;
    using System.Globalization;
    using dddlib.Runtime;

    internal class AggregateRootType
    {
        public AggregateRootType(Type runtimeType, Delegate uninitializedFactory, IEventDispatcher eventDispatcher)
        {
            Guard.Against.Null(() => runtimeType);

            if (!runtimeType.InheritsFrom(typeof(AggregateRoot)))
            {
                throw new RuntimeException(
                    string.Format(CultureInfo.InvariantCulture, "The specified runtime type '{0}' is not an aggregate root.", runtimeType));
            }

            var uninitializedFactoryType = typeof(Func<>).MakeGenericType(runtimeType);
            if (uninitializedFactory != null && uninitializedFactoryType != uninitializedFactory.GetType())
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Invalid uninitialized factory. The specified uninitialized factory of type '{0}' does not match the required type of '{1}'.",
                        uninitializedFactory.GetType(),
                        uninitializedFactoryType));
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

        public Delegate UninitializedFactory { get; private set; }

        public IEventDispatcher EventDispatcher { get; private set; }

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
