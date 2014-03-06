// <copyright file="AggregateRootAnalyzer.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime.Analyzer
{
    using System;

    internal class AggregateRootAnalyzer
    {
        public RuntimeAggregateRoot GetRuntimeType(Type type, TypeConfiguration configuration)
        {
            if (!typeof(AggregateRoot).IsAssignableFrom(type))
            {
                throw new Exception();
            }

            // TODO (Cameron): This should verify that event dispatching is required on the aggregate.
            var dispatchEvents = true;

            var equalityComparer = new TypeAnalyzer().GetDescriptor(type, configuration).EqualityComparer;

            var runtimeType = new RuntimeAggregateRoot
            {
                Factory = configuration.AggregateRootFactory,
                EqualityComparer = equalityComparer,
                EventDispatcher = new DefaultEventDispatcher(type),
                Options = new RuntimeAggregateRoot.RuntimeOptions
                {
                    PersistEvents = configuration.AggregateRootFactory == null,
                    DispatchEvents = dispatchEvents,
                },
            };

            return runtimeType;
        }
    }
}
