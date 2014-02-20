// <copyright file="AssemblyAnalyzer.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    internal class AssemblyAnalyzer
    {
        public AssemblyDescriptor GetDescriptor(Assembly assembly)
        {
            var descriptor = new AssemblyDescriptor();
            descriptor.EventDispatcherFactory = new DefaultEventDispatcherFactory();
            descriptor.AggregateRootFactories = new Dictionary<Type, Func<object>>();

            var bootstrapperTypes = assembly.GetTypes().Where(type => typeof(IBootstrapper).IsAssignableFrom(type));
            if (!bootstrapperTypes.Any())
            {
                // TODO (Cameron): Log?
                return descriptor;
            }

            if (bootstrapperTypes.Count() > 1)
            {
                descriptor.Add("The assembly '{0}' has more than one bootstrapper defined.", assembly.GetName().Name);
            }

            var bootstrapperType = bootstrapperTypes.First();
            var bootstrapper = default(IBootstrapper);
            try
            {
                bootstrapper = (IBootstrapper)Activator.CreateInstance(bootstrapperType);
            }
            catch (Exception ex)
            {
                descriptor.Add(ex, "The bootstrapper of type '{0}' threw an exception during instantiation.", bootstrapperType.Name);
            }

            var configuration = new Configuration(descriptor);
            try
            {
                bootstrapper.Bootstrap((IApplication)configuration);
                bootstrapper.Bootstrap((IDomain)configuration);
            }
            catch (Exception ex)
            {
                descriptor.Add(ex, "The bootstrapper of type '{0}' threw an exception during instantiation.", bootstrapperType.Name);
            }

            return descriptor;
        }

        private class Configuration : IApplication, IDomain
        {
            private readonly AssemblyDescriptor descriptor;

            public Configuration(AssemblyDescriptor descriptor)
            {
                this.descriptor = descriptor;
            }

            public void SetEventDispatcherFactory(IEventDispatcherFactory eventDispatcherFactory)
            {
                this.descriptor.EventDispatcherFactory = eventDispatcherFactory;
            }

            public void RegisterFactory<T>(Func<T> aggregateFactory) where T : AggregateRoot
            {
                Guard.Against.Null(() => aggregateFactory);

                if (this.descriptor.AggregateRootFactories.Any(kvp => kvp.Key == typeof(T)))
                {
                    this.descriptor.Add("The application already has a factory registered for the aggregate root of type '{0}'.", typeof(T).Name);
                    return;
                }

                // TODO (Cameron): Some expression voodoo to fix the double enclosing.
                this.descriptor.AggregateRootFactories = this.descriptor.AggregateRootFactories
                    .Concat(new[] { new KeyValuePair<Type, Func<object>>(typeof(T), () => aggregateFactory()) })
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }
        }
    }
}
