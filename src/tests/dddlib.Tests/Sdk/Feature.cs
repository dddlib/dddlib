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
                    // aggregate root
                    var aggregateRootConfigurationProviders = new IConfigurationProvider<AggregateRootConfiguration>[]
                    {
                        new Bootstrapper(this.Bootstrap),
                        new AggregateRootAnalyzer(),
                    };

                    var aggregateRootConfigurationManager = new AggregateRootConfigurationManager();
                    var aggregateRootConfigurationProvider = new DefaultConfigurationProvider<AggregateRootConfiguration>(aggregateRootConfigurationProviders, aggregateRootConfigurationManager);
                    var aggregateRootTypeFactory = new AggregateRootTypeFactory(aggregateRootConfigurationProvider);

                    // entity
                    var entityConfigurationProviders = new IConfigurationProvider<EntityConfiguration>[]
                    {
                        new Bootstrapper(this.Bootstrap),
                        new EntityAnalyzer(),
                    };

                    var entityConfigurationManager = new EntityConfigurationManager();
                    var entityConfigurationProvider = new DefaultConfigurationProvider<EntityConfiguration>(entityConfigurationProviders, entityConfigurationManager);
                    var entityTypeFactory = new EntityTypeFactory(entityConfigurationProvider);

                    // value object
                    var valueObjectConfigurationProviders = new IConfigurationProvider<ValueObjectConfiguration>[]
                    {
                        new Bootstrapper(this.Bootstrap),
                        //// new ValueObjectAnalyzer(),
                    };

                    var valueObjectConfigurationManager = new ValueObjectConfigurationManager();
                    var valueObjectConfigurationProvider = new DefaultConfigurationProvider<ValueObjectConfiguration>(valueObjectConfigurationProviders, valueObjectConfigurationManager);
                    var valueObjectTypeFactory = new ValueObjectTypeFactory(valueObjectConfigurationProvider);

                    new Application(
                        aggregateRootTypeFactory,
                        entityTypeFactory,
                        valueObjectTypeFactory)
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
