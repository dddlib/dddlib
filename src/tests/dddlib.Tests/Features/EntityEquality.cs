// <copyright file="EntityEquality.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Features
{
    using System;
    using System.Collections.Generic;
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
          X with natural key selector (defined in both bootstrapper and metadata - same)
          X with natural key selector (defined in both bootstrapper and metadata - different)

            Entity Equality (special case: string)
            --------------------------------------
          X with natural key selector (defined - doesn't matter how) AND with natural key equality comparer (undefined)
          X with natural key selector (defined - doesn't matter how) AND with natural key equality comparer (string only, defined in bootstrapper only)

            Entity Equality (special case: composite key value object: strings)
            -------------------------------------------------------------------
          X with natural key selector (defined - doesn't matter how)

            Entity Equality (Inherited)
            ---------------------------
          X with natural key selector (undefined)
          X with natural key selector (undefined in base)
          X with natural key selector (undefined in subclass)
          X with natural key selector (undefined in both base and subclass)

            [all entity equality tests should also work for aggregate roots]
            [consider inheritance]
        */

        public class UndefinedNaturalKeySelector : EntityEquality
        {
            [Scenario]
            public void Scenario(Subject instance1, Subject instance2)
            {
                "Given an entity with an undefined natural key selector"
                    .Given(() => { });

                "When two instances of that entity are instantiated"
                    .When(() =>
                    {
                        instance1 = new Subject();
                        instance2 = new Subject();
                    });

                "Then the first instance is not equal to the second instance"
                    .Then(() => instance1.Should().NotBe(instance2));
            }

            public class Subject : Entity
            {
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
            [Scenario]
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

        public class CaseSensitiveUndefinedEqualityComparer : EntityEquality
        {
            [Scenario]
            public void Scenario(Subject instance1, Subject instance2, string naturalKey)
            {
                "Given a value object with an undefined equality comparer"
                    .Given(() => { });

                "And a natural key value"
                    .And(() => naturalKey = "key");

                "When two instances of that value object that are instantiated with different values"
                    .When(() =>
                    {
                        instance1 = new Subject { NaturalKey = naturalKey.ToUpperInvariant() };
                        instance2 = new Subject { NaturalKey = naturalKey.ToLowerInvariant() };
                    });

                "Then the first instance is equal to the second instance"
                    .Then(() => instance1.Should().NotBe(instance2));
            }

            public class Subject : Entity
            {
                [NaturalKey]
                public string NaturalKey { get; set; }
            }
        }

        public class CaseInsensitiveEqualityComparerDefinedInBootstrapper : EntityEquality
        {
            [Scenario]
            public void Scenario(Subject instance1, Subject instance2, string naturalKey)
            {
                "Given a value object with an undefined equality comparer"
                    .Given(() => { });

                "And a natural key value"
                    .And(() => naturalKey = "key");

                "When two instances of that value object that are instantiated with different values"
                    .When(() =>
                    {
                        instance1 = new Subject { NaturalKey = new Key { Value = naturalKey.ToUpperInvariant() } };
                        instance2 = new Subject { NaturalKey = new Key { Value = naturalKey.ToLowerInvariant() } };
                    });

                "Then the first instance is equal to the second instance"
                    .Then(() => instance1.Should().Be(instance2));
            }

            public class Key : ValueObject<Key>
            {
                public string Value { get; set; }
            }

            public class Subject : Entity
            {
                public Key NaturalKey { get; set; }
            }

            private class BootStrapper : IBootstrap<Subject>, IBootstrap<Key>
            {
                public void Bootstrap(IConfiguration configure)
                {
                    configure.Entity<Subject>().ToUseNaturalKey(subject => subject.NaturalKey);
                    configure.ValueObject<Key>().ToUseEqualityComparer(new KeyEqualityComparer());
                }

                private class KeyEqualityComparer : IEqualityComparer<Key>
                {
                    public bool Equals(Key x, Key y)
                    {
                        return string.Equals(x.Value, y.Value, StringComparison.OrdinalIgnoreCase);
                    }

                    public int GetHashCode(Key obj)
                    {
                        return obj.GetHashCode();
                    }
                }
            }
        }

        public class CaseSensitiveCompositeNaturalKeyEqualityComparer : EntityEquality
        {
            [Scenario]
            public void Scenario(Subject instance1, Subject instance2, string component1, string component2)
            {
                "Given a value object with an undefined equality comparer"
                    .Given(() => { });

                "And a natural key value"
                    .And(() => 
                    {
                        component1 = "key1";
                        component2 = "key2";
                    });

                "When two instances of that value object that are instantiated with different values"
                    .When(() =>
                    {
                        var naturalKey1 = new NaturalKeyValue { Component1 = component1.ToUpperInvariant(), Component2 = component2 };
                        var naturalKey2 = new NaturalKeyValue { Component1 = component1.ToLowerInvariant(), Component2 = component2 };

                        instance1 = new Subject { NaturalKey = naturalKey1 };
                        instance2 = new Subject { NaturalKey = naturalKey2 };
                    });

                "Then the first instance is equal to the second instance"
                    .Then(() => instance1.Should().NotBe(instance2));
            }

            public class NaturalKeyValue : ValueObject<NaturalKeyValue>
            {
                public string Component1 { get; set; }

                public string Component2 { get; set; }
            }

            public class Subject : Entity
            {
                [NaturalKey]
                public NaturalKeyValue NaturalKey { get; set; }
            }
        }

        public class UndefinedNaturalKeySelectorWithInheritance : EntityEquality
        {
            [Scenario]
            public void Scenario(SuperSubject instance1, SuperSubject instance2)
            {
                "Given an entity with an undefined natural key selector"
                    .Given(() => { });

                "When two instances of that entity are instantiated"
                    .When(() =>
                    {
                        instance1 = new SuperSubject();
                        instance2 = new SuperSubject();
                    });

                "Then the first instance is not equal to the second instance"
                    .Then(() => instance1.Should().NotBe(instance2));
            }

            public class Subject : Entity
            {
            }

            public class SuperSubject : Subject
            {
            }
        }

        public class NaturalKeySelectorDefinedInBaseClass : EntityEquality
        {
            [Scenario]
            public void Scenario(Subject instance1, Subject instance2, string naturalKey)
            {
                "Given an entity with a natural key selector defined in the base class"
                    .Given(() => { });

                "And a natural key value"
                    .And(() => naturalKey = "key");

                "When two instances of that entity are instantiated with that natural key value assigned"
                    .When(() =>
                    {
                        instance1 = new SuperSubject { NaturalKey = naturalKey };
                        instance2 = new SuperSubject { NaturalKey = naturalKey };
                    });

                "Then the first instance is equal to the second instance"
                    .Then(() => instance1.Should().Be(instance2));
            }

            public class Subject : Entity
            {
                [NaturalKey]
                public string NaturalKey { get; set; }
            }

            public class SuperSubject : Subject
            {
            }
        }

        public class NaturalKeySelectorDefinedISubclass : EntityEquality
        {
            [Scenario]
            public void Scenario(Subject instance1, Subject instance2, string naturalKey)
            {
                "Given an entity with a natural key selector defined in the subclass"
                    .Given(() => { });

                "And a natural key value"
                    .And(() => naturalKey = "key");

                "When two instances of that entity are instantiated with that natural key value assigned"
                    .When(() =>
                    {
                        instance1 = new SuperSubject { NaturalKey = naturalKey };
                        instance2 = new SuperSubject { NaturalKey = naturalKey };
                    });

                "Then the first instance is equal to the second instance"
                    .Then(() => instance1.Should().Be(instance2));
            }

            public class Subject : Entity
            {
            }

            public class SuperSubject : Subject
            {
                [NaturalKey]
                public string NaturalKey { get; set; }
            }
        }

        public class NaturalKeySelectorDefinedInBothBaseClassAndSubclass : EntityEquality
        {
            [Scenario]
            public void Scenario(Subject instance1, Subject instance2, string naturalKey)
            {
                "Given an entity with a natural key selector defined in the base class and the subclass"
                    .Given(() => { });

                "And a natural key value"
                    .And(() => naturalKey = "key");

                "When two instances of that entity are instantiated with the subclass natural key value assigned"
                    .When(() =>
                    {
                        instance1 = new SuperSubject { NaturalKey = "unequalValue", NaturalKey2 = naturalKey };
                        instance2 = new SuperSubject { NaturalKey = "unequalValue2", NaturalKey2 = naturalKey };
                    });

                "Then the first instance is equal to the second instance"
                    .Then(() => instance1.Should().Be(instance2));
            }

            public class Subject : Entity
            {
                [NaturalKey]
                public string NaturalKey { get; set; }
            }

            public class SuperSubject : Subject
            {
                [NaturalKey]
                public string NaturalKey2 { get; set; }
            }
        }
    }
}
