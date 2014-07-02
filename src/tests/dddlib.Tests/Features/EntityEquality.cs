// <copyright file="EntityEquality.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Features
{
    using System;
    using dddlib.Configuration;
    using dddlib.Runtime;
    using dddlib.Tests.Sdk;
    using FluentAssertions;
    using Xbehave;

    // As someone who uses dddlib
    // In order to persist an aggregate root
    // I need to be able to perform equality operations against entities (as an aggregate root is an entity)
    public abstract class EntityEquality : Feature
    {
        /*
            Entity Equality
            ---------------
          X with natural key selector (undefined)
          X with natural key selector (defined in metadata)
          X with natural key selector (defined in bootstrapper)
            with natural key selector (defined in both bootstrapper and metadata - same)
          X with natural key selector (defined in both bootstrapper and metadata - different)

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
            [consider inheritance]
        */

        public class UndefinedNaturalKeySelector : EntityEquality
        {
            [Scenario]
            public void Scenario(Subject instance1, Subject instance2, string naturalKey)
            {
                "Given an entity with an undefined natural key selector"
                    .Given(() => { });

                "And a natural key value"
                    .And(() => naturalKey = "key");

                "When two instances of that entity are instantiated with that natural key value assigned"
                    .When(() =>
                    {
                        instance1 = new Subject { NaturalKey = naturalKey };
                        instance2 = new Subject { NaturalKey = naturalKey };
                    });

                "Then the first instance is not equal to the second instance"
                    .Then(() => instance1.Should().NotBe(instance2));
            }

            public class Subject : Entity
            {
                public string NaturalKey { get; set; }
            }
        }

        public class NaturalKeySelectorDefinedInMetadata : EntityEquality
        {
            [Scenario]
            public void Scenario(Subject instance1, Subject instance2, string naturalKey)
            {
                "Given an entity with a natural key selector defined in metadata"
                    .Given(() => { });

                "And a natural key value"
                    .And(() => naturalKey = "key");

                "When two instances of that entity are instantiated with that natural key value assigned"
                    .When(() =>
                    {
                        instance1 = new Subject { NaturalKey = naturalKey };
                        instance2 = new Subject { NaturalKey = naturalKey };
                    });

                "Then the first instance is equal to the second instance"
                    .Then(() => instance1.Should().Be(instance2));
            }

            public class Subject : Entity
            {
                [NaturalKey]
                public string NaturalKey { get; set; }
            }
        }

        public class NaturalKeySelectorDefinedInBootstrapper : EntityEquality
        {
            [Scenario]
            public void Scenario(Subject instance1, Subject instance2, string naturalKey)
            {
                "Given an entity with a natural key selector defined in the bootstrapper"
                    .Given(() => { });

                "And a natural key value"
                    .And(() => naturalKey = "key");

                "When two instances of that entity are instantiated with that natural key value assigned"
                    .When(() =>
                    {
                        instance1 = new Subject { NaturalKey = naturalKey };
                        instance2 = new Subject { NaturalKey = naturalKey };
                    });

                "Then the first instance is equal to the second instance"
                    .Then(() => instance1.Should().Be(instance2));
            }

            public class Subject : Entity
            {
                public string NaturalKey { get; set; }
            }

            private class BootStrapper : IBootstrap<Subject>
            {
                public void Bootstrap(IConfiguration configure)
                {
                    configure.Entity<Subject>().ToUseNaturalKey(subject => subject.NaturalKey);
                }
            }
        }

        public class NonConflictingNaturalKeySelectors : EntityEquality
        {
            [Scenario] // (Skip = "Doesn't work yet!")]
            public void Scenario(Subject instance1, Subject instance2, string naturalKey)
            {
                "Given an entity with non-conflicting natural key selectors defined in both metadata and the bootstrapper"
                    .Given(() => { });

                "And a natural key value"
                    .And(() => naturalKey = "key");

                "When two instances of that entity are instantiated with that natural key value assigned"
                    .When(() =>
                    {
                        instance1 = new Subject { NaturalKey = naturalKey };
                        instance2 = new Subject { NaturalKey = naturalKey };
                    });

                "Then the first instance is equal to the second instance"
                    .Then(() => instance1.Should().Be(instance2));
            }

            public class Subject : Entity
            {
                [NaturalKey]
                public string NaturalKey { get; set; }
            }

            private class BootStrapper : IBootstrap<Subject>
            {
                public void Bootstrap(IConfiguration configure)
                {
                    configure.Entity<Subject>().ToUseNaturalKey(subject => subject.NaturalKey);
                }
            }
        }

        public class ConflictingNaturalKeySelectors : EntityEquality
        {
            [Scenario]
            public void Scenario(Type type, Action action)
            {
                "Given an entity with conflicting natural key selectors defined in both metadata and the bootstrapper"
                    .Given(() => { });

                "When an instance of that entity is instantiated"
                    .When(() => action = () => new Subject());

                "Then a runtime exception should be thrown" // and the runtime exception should state that the natural key is defined twice
                    .Then(() => action.ShouldThrow<RuntimeException>());
            }

            public class Subject : Entity
            {
                [NaturalKey]
                public string FirstNaturalKey { get; set; }

                public string SecondNaturalKey { get; set; }
            }

            private class BootStrapper : IBootstrap<Subject>
            {
                public void Bootstrap(IConfiguration configure)
                {
                    configure.Entity<Subject>().ToUseNaturalKey(subject => subject.SecondNaturalKey);
                }
            }
        }
    }
}
