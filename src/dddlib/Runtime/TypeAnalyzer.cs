// <copyright file="TypeAnalyzer.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;

    // TODO (Cameron): Test that the Natural key is from the most recent subclass of entity.
    internal class TypeAnalyzer
    {
        private readonly Configuration configuration;

        public TypeAnalyzer(Configuration configuration)
        {
            this.configuration = configuration;
        }

        public TypeDescriptor GetDescriptor(Type type)
        {
            var descriptor = new TypeDescriptor();

            if (typeof(AggregateRoot).IsAssignableFrom(type))
            {
                try
                {
                    descriptor.EventDispatcher = this.configuration.EventDispatcherFactory.CreateEventDispatcher(type);
                }
                catch (Exception ex)
                {
                    throw new RuntimeException(
                        string.Format(
                            CultureInfo.InvariantCulture, 
                            "The event dispatcher factory of type '{0}' threw an exception during invocation.", 
                            type.Name),
                        ex);
                }
            }

            if (typeof(Entity).IsAssignableFrom(type))
            {
                var naturalKey = default(NaturalKeyAttribute);
                foreach (var subType in new[] { type }.Traverse(t => t.BaseType == typeof(Entity) ? null : new[] { t.BaseType }))
                {
                    naturalKey = subType.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public)
                    .SelectMany(member => member.GetCustomAttributes(typeof(NaturalKeyAttribute), true))
                    .OfType<NaturalKeyAttribute>()
                    .SingleOrDefault();

                    if (naturalKey != null)
                    {
                        break;
                    }
                }

                if (naturalKey == null)
                {
                    throw new RuntimeException(
                        string.Format(CultureInfo.InvariantCulture, "The entity of type '{0}' does not have a natural key defined.", type.Name));
                }

                var equalityComparerType = naturalKey.EqualityComparer;
                if (equalityComparerType == null)
                {
                    descriptor.EqualityComparer = EqualityComparer<object>.Default;
                    return descriptor;
                }

                var equalityComparer = default(IEqualityComparer<object>);
                try
                {
                    equalityComparer = (IEqualityComparer<object>)Activator.CreateInstance(equalityComparerType);
                }
                catch (Exception ex)
                {
                    throw new RuntimeException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "The equality comparer of type '{0}' threw an exception during instantiation.",
                            type.Name),
                        ex);
                }

                // TODO (Cameron): Ensure equality comparer instantiation is safe. ie. error-handled correctly.
                descriptor.EqualityComparer = equalityComparer;
            }

            return descriptor;
        }
    }
}
