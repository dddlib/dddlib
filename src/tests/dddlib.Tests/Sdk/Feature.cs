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
    using dddlib.Sdk;
    using dddlib.Sdk.Configuration;
    using dddlib.Sdk.Configuration.Model;
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
                    var typeAnalyzerService = new DefaultTypeAnalyzerService();
                    var bootstrapperProvider = new FeatureBootstrapperProvider();
                    new Application(
                        t => new AggregateRootTypeFactory(typeAnalyzerService, bootstrapperProvider).Create(t),
                        t => new EntityTypeFactory(typeAnalyzerService, bootstrapperProvider).Create(t),
                        t => new ValueObjectTypeFactory(typeAnalyzerService, bootstrapperProvider).Create(t))
                        .Using();
                });
        }

        private static ValueObjectType CreateValueObjectType(ITypeAnalyzerService typeAnalyzerService, IBootstrapperProvider bootstrapperProvider, Type type)
        {
            return new ValueObjectTypeFactory(typeAnalyzerService, bootstrapperProvider).Create(type);
        }

        private class FeatureBootstrapperProvider : IBootstrapperProvider
        {
            // TODO (Cameron): This is all a bit of a hack.
            public Action<IConfiguration> GetBootstrapper(Type type)
            {
                var bootstrappers = type.DeclaringType == null
                    ? new Type[0]
                    : type.DeclaringType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                        .Where(t => t.GetInterfaces()
                            .Any(@interface => @interface.IsGenericType && @interface.GetGenericTypeDefinition() == typeof(IBootstrap<>)))
                        .ToArray();

                var bootstrapperType = bootstrappers.SingleOrDefault(t => t.GetInterfaces().Any(i => i.GetGenericArguments()[0] == type));
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
}
