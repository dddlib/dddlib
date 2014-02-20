// <copyright file="TypeAnalyzer.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    internal class TypeAnalyzer
    {
        private readonly AssemblyDescriptor assemblyDescriptor;

        public TypeAnalyzer(AssemblyDescriptor assemblyDescriptor)
        {
            this.assemblyDescriptor = assemblyDescriptor;
        }

        public TypeDescriptor GetDescriptor(Type type)
        {
            var descriptor = new TypeDescriptor();

            if (typeof(AggregateRoot).IsAssignableFrom(type))
            {
                descriptor.IsAggregateRoot = true;
                descriptor.EventDispatcher = this.assemblyDescriptor.EventDispatcherFactory.CreateEventDispatcher(type);
            }

            if (typeof(Entity).IsAssignableFrom(type))
            {
                var naturalKey = type.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public)
                    .SelectMany(member => member.GetCustomAttributes(typeof(NaturalKeyAttribute), false))
                    .OfType<NaturalKeyAttribute>()
                    .SingleOrDefault();

                if (naturalKey == null)
                {
                    descriptor.Add("The entity of type '{0}' does not have a natural key defined.", type.Name);
                    
                    descriptor.IsEntity = true;
                    descriptor.EqualityComparer = EqualityComparer<object>.Default;

                    return descriptor;
                }

                var equalityComparerType = naturalKey.EqualityComparer;
                var equalityComparer = default(IEqualityComparer<object>);
                try
                {
                    equalityComparer = (IEqualityComparer<object>)Activator.CreateInstance(equalityComparerType);
                }
                catch (Exception ex)
                {
                    descriptor.Add(ex, "The equality comparer of type '{0}' threw an exception during instantiation.", equalityComparerType.Name);
                }

                // TODO (Cameron): Ensure equality comparer instantiation is safe. ie. error-handled correctly.
                descriptor.IsEntity = true;
                descriptor.EqualityComparer = equalityComparer ?? EqualityComparer<object>.Default;
            }

            return descriptor;
        }
    }
}
