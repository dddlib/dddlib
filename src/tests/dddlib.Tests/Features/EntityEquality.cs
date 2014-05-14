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

    public abstract class EntityEquality : Feature
    {
        /*
            Entity Equality
            ---------------
          X with natural key selector (undefined)
          X with natural key selector (defined in bootstrapper)
          X with natural key selector (defined in metadata)
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
            [consider inheritence]
        */

        public class UndefinedNaturalKeySelector : EntityEquality
        {
            [Scenario]
            public void Scenario(Subject instance1, Subject instance2, bool consideredEqual)
            {
                "Given two instances of an entity with an undefined natural key selector"
                    .Given(() =>
                    {
                        instance1 = new Subject();
                        instance2 = new Subject();
                    });

                "When the instances are compared for equality"
                    .When(() => consideredEqual = instance1 == instance2);

                "Then they are not considered equal"
                    .Then(() => consideredEqual.Should().BeFalse());
            }

            public class Subject : Entity
            {
            }
        }

        public class NaturalKeySelectorDefinedInBootstrapper : EntityEquality
        {
            [Scenario]
            public void Scenario(Subject instance1, Subject instance2, bool consideredEqual)
            {
                "Given two instances of an entity with an undefined natural key selector"
                    .Given(() =>
                    {
                        instance1 = new Subject { NaturalKey = "key" };
                        instance2 = new Subject { NaturalKey = "key" };
                    });

                "When the instances are compared for equality"
                    .When(() => consideredEqual = instance1 == instance2);

                "Then they are considered equal"
                    .Then(() => consideredEqual.Should().BeTrue());
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

        public class NaturalKeySelectorDefinedInMetadata : EntityEquality
        {
            [Scenario]
            public void Scenario(Subject instance1, Subject instance2, bool consideredEqual)
            {
                "Given two instances of an entity with an undefined natural key selector"
                    .Given(() =>
                    {
                        instance1 = new Subject { NaturalKey = "key" };
                        instance2 = new Subject { NaturalKey = "key" };
                    });

                "When the instances are compared for equality"
                    .When(() => consideredEqual = instance1 == instance2);

                "Then they are considered equal"
                    .Then(() => consideredEqual.Should().BeTrue());
            }

            public class Subject : Entity
            {
                [NaturalKey]
                public string NaturalKey { get; set; }
            }
        }

        public class NonConflictingNaturalKeySelectors : EntityEquality
        {
            [Scenario(Skip = "Doesn't work yet!")]
            public void Scenario(Subject instance1, Subject instance2, bool consideredEqual)
            {
                "Given two instances of an entity with duplicate non-conflicting natural key selectors"
                    .Given(() =>
                    {
                        instance1 = new Subject { NaturalKey = "key" };
                        instance2 = new Subject { NaturalKey = "key" };
                    });

                "When the instances are compared for equality"
                    .When(() => consideredEqual = instance1 == instance2);

                "Then they are considered equal"
                    .Then(() => consideredEqual.Should().BeTrue());
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
                "When an instance of an entity with conflicting natural key selectors is instantiated"
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
