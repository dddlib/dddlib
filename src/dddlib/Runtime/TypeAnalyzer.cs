// <copyright file="TypeAnalyzer.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    /*  TODO (Cameron): 
        This is it. This is where the magic happens. Fix last. Test first?
        Consider seperate calls, maybe seperate classes? GetEntity, GetValueObject etc.
        Fix exceptions (see previous).
        Consider: do not require the type as an argument.  */

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;

    // TODO (Cameron): Test that the Natural key is from the most recent subclass of entity.
    [Obsolete]
    internal class TypeAnalyzer : ITypeAnalyzer2
    {
        private static readonly NullEventDispatcher DefaultEventDispatcher = new NullEventDispatcher();

        public TypeDescriptor GetDescriptor(Type type, TypeConfiguration configuration)
        {
            var descriptor = new TypeDescriptor();

            if (typeof(AggregateRoot).IsAssignableFrom(type))
            {
                if (configuration.EventDispatcher != null)
                {
                    try
                    {
                        descriptor.EventDispatcher = configuration.EventDispatcher;
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
                else
                {
                    descriptor.EventDispatcher = DefaultEventDispatcher;
                }

                descriptor.Factory = configuration.AggregateRootFactory;
            }

            if (typeof(Entity).IsAssignableFrom(type))
            {
                var naturalKey = default(NaturalKey);
                foreach (var subType in new[] { type }.Traverse(t => t.BaseType == typeof(Entity) ? null : new[] { t.BaseType }))
                {
                    naturalKey = subType.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public)
                        .SelectMany(member => member.GetCustomAttributes(typeof(NaturalKey), true))
                        .OfType<NaturalKey>()
                        .SingleOrDefault();

                    if (naturalKey != null)
                    {
                        break;
                    }
                }

                if (naturalKey == null)
                {
                    if (configuration.AggregateRootFactory != null)
                    {
                        throw new RuntimeException(
                            string.Format(CultureInfo.InvariantCulture, "The entity of type '{0}' does not have a natural key defined.", type.Name));
                    }

                    descriptor.EqualityComparer = EqualityComparer<object>.Default;
                    return descriptor;
                }

                var equalityComparerType = default(object);
                if (equalityComparerType == null)
                {
                    descriptor.EqualityComparer = EqualityComparer<object>.Default;
                    return descriptor;
                }

                var equalityComparer = default(IEqualityComparer<object>);
                try
                {
                    // equalityComparer = (IEqualityComparer<object>)Activator.CreateInstance(equalityComparerType);
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

        private class NullEventDispatcher : ITargetedEventDispatcher
        {
            public void Dispatch(object target, object @event)
            {
            }
        }
    }
}
