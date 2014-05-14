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
                    var configurationProviders = new IConfigurationProvider<EntityConfiguration>[]
                    {
                        new Bootstrapper(this.Bootstrap),
                        new EntityAnalyzer(),
                    };

                    var configurationManager = new EntityConfigurationManager();
                    var configurationProvider = new DefaultConfigurationProvider<EntityConfiguration>(configurationProviders, configurationManager);
                    var entityTypeFactory = new EntityTypeFactory(configurationProvider);

                    new Application(
                        A.Fake<ITypeFactory<AggregateRootType>>(o => o.Strict()),
                        entityTypeFactory,
                        A.Fake<ITypeFactory<ValueObjectType>>(o => o.Strict()))
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
