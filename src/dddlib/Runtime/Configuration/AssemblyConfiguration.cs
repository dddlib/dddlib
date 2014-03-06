// <copyright file="AssemblyConfiguration.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime.Configuration
{
    using System;
    using System.Collections.Generic;

    // TODO (Cameron): Make public
    internal class AssemblyConfiguration
    {
        private readonly Dictionary<Type, Func<object>> aggregateRootFactories = new Dictionary<Type, Func<object>>();
        private readonly Dictionary<Type, Func<object, object>> naturalKeySelectors = new Dictionary<Type, Func<object, object>>();

        private bool dispatchEvents = true;

        public void DoNotDispatchEvents()
        {
            this.dispatchEvents = false;
        }

        public void RegisterAggregateRootFactory<T>(Func<T> aggregateRootFactory) where T : AggregateRoot
        {
            Guard.Against.Null(() => aggregateRootFactory);

            if (!this.dispatchEvents)
            {
                throw new RuntimeException();
            }

            // TODO (Cameron): Some exception related stuff to not allow multiple additions for same key.
            // TODO (Cameron): Some expression magic.
            this.aggregateRootFactories.Add(typeof(T), () => aggregateRootFactory());
        }

        public void RegisterNaturalKeySelector<T, TKey>(Func<T, TKey> naturalKeySelector) where T : AggregateRoot
        {
            Guard.Against.Null(() => naturalKeySelector);
            
            // TODO (Cameron): Some exception related stuff to not allow multiple additions for same key.
            // TODO (Cameron): Some expression magic.
            this.naturalKeySelectors.Add(typeof(T), aggregateRoot => naturalKeySelector((T)aggregateRoot));
        }

        internal TypeConfiguration CreateConfiguration(Type type)
        {
            var aggregateRootFactory = default(Func<object>);
            if (this.aggregateRootFactories.TryGetValue(type, out aggregateRootFactory))
            {
                return new TypeConfiguration
                {
                    EventDispatcher = new DefaultEventDispatcher(type),
                    AggregateRootFactory = aggregateRootFactory,
                };
            }

            return new TypeConfiguration
            {
                EventDispatcher = new DefaultEventDispatcher(type),
            };
        }
    }
}
