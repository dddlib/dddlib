// <copyright file="Feature.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Sdk
{
    using System;
    using System.Linq;
    using System.Reflection;
    using dddlib.Configuration;
    using dddlib.Runtime;
    using FakeItEasy;
    using Xbehave;

    public abstract class Feature
    {
        protected interface IBootstrap<T>
        {
            void Bootstrap(IConfiguration configure);
        }

        [Background]
        public void Background()
        {
            "Given a new application"
                .Given(() =>
                {
                    var mapper = new Mapper();

                    // aggregate root
                    var aggregateRootConfigurationManager = new AggregateRootConfigurationManager();
                    var aggregateRootConfigurationProvider = new DefaultConfigurationProvider<AggregateRootConfiguration>(
                        new Func<Type, AggregateRootConfiguration>[]
                        {
                            t => ((IAggregateRootConfigurationProvider)new Bootstrapper(this.Bootstrap, mapper)).GetConfiguration(t),
                            t => new AggregateRootAnalyzer().GetConfiguration(t),
                        },
                        aggregateRootConfigurationManager);

                    var aggregateRootTypeFactory = new AggregateRootTypeFactory_Old(new Application.InternalAggregateRootConfigurationProvider(aggregateRootConfigurationProvider));

                    // entity
                    var entityConfigurationManager = new EntityConfigurationManager();
                    var entityConfigurationProvider = new DefaultConfigurationProvider<EntityConfiguration>(
                        new Func<Type, EntityConfiguration>[]
                        {
                            t => ((IEntityConfigurationProvider)new Bootstrapper(this.Bootstrap, mapper)).GetConfiguration(t),
                            t => new EntityAnalyzer().GetConfiguration(t),
                        },
                        entityConfigurationManager);

                    var entityTypeFactory = new EntityTypeFactory_Old(new Application.InternalEntityConfigurationProvider(entityConfigurationProvider));

                    // value object
                    var valueObjectConfigurationProviders = new IValueObjectConfigurationProvider[]
                    {
                        new Bootstrapper(this.Bootstrap, mapper),
                        //// new ValueObjectAnalyzer(),
                    };

                    var valueObjectConfigurationManager = new ValueObjectConfigurationManager();
                    var valueObjectConfigurationProvider = new ValueObjectConfigurationProvider(new Bootstrapper(this.Bootstrap, mapper), new ValueObjectAnalyzer(), new ValueObjectConfigurationManager());
                    var valueObjectTypeFactory = new ValueObjectTypeFactory_Old(valueObjectConfigurationProvider);

                    new Application(
                        aggregateRootTypeFactory,
                        entityTypeFactory,
                        valueObjectTypeFactory,
                        mapper)
                        .Using();
                });
        }

        // TODO (Cameron): This is all a bit of a hack.
        private Action<IConfiguration> Bootstrap(Type type)
        {
            var bootstrappers = this.GetType().GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                .Where(t => t.GetInterfaces()
                    .Any(@interface => @interface.IsGenericType && @interface.GetGenericTypeDefinition() == typeof(IBootstrap<>)))
                .ToArray();

            var bootstrapperType = bootstrappers.SingleOrDefault(t => t.GetInterfaces()[0].GetGenericArguments()[0] == type);
            if (bootstrapperType == null)
            {
                return c => { };
            }

            var bootstrapper = Activator.CreateInstance(bootstrapperType);
            var method = bootstrapperType.GetMethod("Bootstrap");

            Action<IConfiguration> action = (Action<IConfiguration>)Delegate.CreateDelegate(typeof(Action<IConfiguration>), bootstrapper, method);

            return action; //// (Action<IConfiguration>)bootstrapper.Bootstrap;
        }
    }
}
