// <copyright file="EntityEqualityFeature.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Acceptance
{
    using System;
    using dddlib.Configuration;
    using dddlib.Runtime;
    using dddlib.Tests.Sdk;
    using FluentAssertions;
    using Xbehave;

    public class EntityEqualityFeature
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

        public class UndefinedNaturalKeySelector
        {
            ////[Scenario]
            public void EntityTests(Type type)
            {
                var entityType = default(EntityType);

                "Given a type with an undefined natural key selector"
                    .Given(() => type = typeof(Subject));

                "When the application is used to get the entity type"
                    .When(() => entityType = Application.Current.GetEntityType(type));

                "Then the entity type natural key selector should be null"
                    .Then(() => entityType.NaturalKeySelector.Should().BeNull());
            }

            public class Subject : Entity
            {
                public string NaturalKey { get; set; }
            }
        }

        public class ConflictingNaturalKeySelectors
        {
            ////[Scenario]
            public void EntityTests(Type type, Action action)
            {
                "Given an instance of the ambient application"
                    .Given(() =>
                    {
                        new Application().Using();
                    });

                "And a type with conflicting natural key selectors"
                    .And(() => type = typeof(Subject));

                "When the application is used to get the entity type"
                    .When(() => action = () => Application.Current.GetEntityType(type));

                "Then a runtime exception should be thrown"
                    .Then(() => action.ShouldThrow<RuntimeException>());
            }

            public class Subject : Entity
            {
                [NaturalKey]
                public string NaturalKey { get; set; }

                public string NotNaturalKey { get; set; }
            }

            private class BootStrapper : IBootstrap<Subject>
            {
                public void Bootstrap(IConfiguration configure)
                {
                    configure.Entity<Subject>().ToUseNaturalKey(subject => subject.NotNaturalKey);
                }
            }
        }
    }
}
