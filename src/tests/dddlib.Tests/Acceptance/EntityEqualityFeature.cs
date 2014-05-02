// <copyright file="EntityEqualityFeature.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Acceptance
{
    using System;
    using System.Linq;
    using System.Reflection;
    using dddlib.Configuration;
    using dddlib.Runtime;
    using dddlib.Tests.Sdk;
    using FakeItEasy;
    using FluentAssertions;
    using Xbehave;

    public abstract class EntityEqualityFeature
    {
        /*
            Entity Equality
            ---------------
            with natural key selector (undefined)
            with natural key selector (defined in bootstrapper)
            with natural key selector (defined in metadata)
            with natural key selector (defined in both bootstrapper and metadata - same)
            with natural key selector (defined in both bootstrapper and metadata - different)

            Entity Equality (special case: string)
            --------------------------------------
            with natural key selector (defined - doesn't matter how) AND with natural key equality comparer (undefined)
            with natural key selector (defined - doesn't matter how) AND with natural key equality comparer (string only, defined in bootstrapper only)

            Entity Equality (special case: composite key value object: strings)
            -------------------------------------------------------------------
            with natural key selector (defined - doesn't matter how)

            Entity Equality (Inherited)
            ---------------------------
            with natural key selector (undefined)
            with natural key selector (undefined in base)
            with natural key selector (undefined in subclass)

            [all entity equality tests should also work for aggregate roots]
            [consider inheritence]
        */

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

        public class UndefinedNaturalKeySelector : EntityEqualityFeature
        {
            [Scenario]
            public void UndefinedNaturalKeySelectorScenario(Subject instance1, Subject instance2, bool areEqual)
            {
                "Given two instances of an entity with an undefined natural key selector"
                    .Given(() => 
                    {
                        instance1 = new Subject();
                        instance2 = new Subject();
                    });

                "When the instances are compared for equality"
                    .When(() => areEqual = instance1 == instance2);

                "Then they are not considered equal"
                    .Then(() => areEqual.Should().BeFalse());
            }

            public class Subject : Entity
            {
                public string NaturalKey { get; set; }
            }
        }

        public class ConflictingNaturalKeySelectors : EntityEqualityFeature
        {
            [Scenario]
            public void ConflictingNaturalKeySelectorsScenario(Type type, Action action)
            {
                "When an instance of an entity with conflicting natural key selectors is instantiated"
                    .When(() => action = () => new Subject());

                "Then a runtime exception should be thrown" // and the runtime exception should state that the natural key is defined twice
                    .Then(() => action.ShouldThrow<RuntimeException>());
            }

            public class Subject : Entity
            {
                [NaturalKey]
                public string NaturalKey { get; set; }

                public string NotNaturalKey { get; set; }
            }

            // NOTE (Cameron): This makes sense because we'll need multiples of this when we address inherited types.
            private class BootStrapper : IBootstrap<Subject>
            {
                public void Bootstrap(IConfiguration configure)
                {
                    // NOTE (Cameron): Configuration goes here using the 'configure' parameter.
                    configure.Entity<Subject>().ToUseNaturalKey(subject => subject.NotNaturalKey);
                }
            }
        }
    }
}
