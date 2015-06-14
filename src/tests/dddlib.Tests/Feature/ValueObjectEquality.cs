// <copyright file="ValueObjectEquality.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Feature
{
    using System;
    using System.Collections.Generic;
    using dddlib.Configuration;
    using dddlib.Tests.Sdk;
    using FluentAssertions;
    using Xbehave;

    // As someone who uses dddlib
    // In order to persist an aggregate root with a value object for a natural key
    // I need to be able to perform equality operations against value objects
    public abstract class ValueObjectEquality : Feature
    {
        /*
            Value Object Equality
            ---------------------
            Inheritance.
        */

        public class UndefinedEqualityComparer : ValueObjectEquality
        {
            [Scenario]
            public void Scenario(Subject instance1, Subject instance2, string value)
            {
                "Given a value object with an undefined equality comparer"
                    .f(() => { });

                "And a value"
                    .f(() => value = "key");

                "When two instances of that value object that are instantiated with the same value"
                    .f(() =>
                    {
                        instance1 = new Subject { Value = value };
                        instance2 = new Subject { Value = value };
                    });

                "Then the first instance is equal to the second instance"
                    .f(() => instance1.Should().Be(instance2));
            }

            public class Subject : ValueObject<Subject>
            {
                public string Value { get; set; }
            }
        }

        public class EqualityComparerDefinedInBootstrapper : ValueObjectEquality
        {
            [Scenario]
            public void Scenario(Subject instance1, Subject instance2)
            {
                "Given a value object with an equality comparer defined in the bootstrapper"
                    .f(() => { });

                "When two instances of that value object that are instantiated with the 'same' value"
                    .f(() =>
                    {
                        instance1 = new Subject { Value = "a" };
                        instance2 = new Subject { Value = "b" };
                    });

                "Then the first instance is equal to the second instance"
                    .f(() => instance1.Should().Be(instance2));
            }

            public class Subject : ValueObject<Subject>
            {
                public string Value { get; set; }
            }

            private class BootStrapper : IBootstrap<Subject>
            {
                public void Bootstrap(IConfiguration configure)
                {
                    configure.ValueObject<Subject>().ToUseEqualityComparer(new EqualityComparer());
                }
            }

            private class EqualityComparer : IEqualityComparer<Subject>
            {
                public bool Equals(Subject x, Subject y)
                {
                    return x.Value == "a" && y.Value == "b";
                }

                public int GetHashCode(Subject obj)
                {
                    return obj.Value.GetHashCode();
                }
            }
        }

        public class CaseSensitiveUndefinedEqualityComparer : ValueObjectEquality
        {
            [Scenario]
            public void Scenario(Subject instance1, Subject instance2, string value)
            {
                "Given a value object with an undefined equality comparer"
                    .f(() => { });

                "When two instances of that value object that are instantiated with different values"
                    .f(() =>
                    {
                        instance1 = new Subject { Value = "CASE" };
                        instance2 = new Subject { Value = "case" };
                    });

                "Then the first instance is equal to the second instance"
                    .f(() => instance1.Should().NotBe(instance2));
            }

            public class Subject : ValueObject<Subject>
            {
                public string Value { get; set; }
            }
        }

        public class CaseInsensitiveStringEqualityComparerDefinedInBootstrapper : ValueObjectEquality
        {
            [Scenario]
            public void Scenario(Subject instance1, Subject instance2)
            {
                "Given a value object with an equality comparer defined in the bootstrapper"
                    .f(() => { });

                "When two instances of that value object that are instantiated with the 'same' value"
                    .f(() =>
                    {
                        instance1 = new Subject { Value = "CASE" };
                        instance2 = new Subject { Value = "case" };
                    });

                "Then the first instance is equal to the second instance"
                    .f(() => instance1.Should().Be(instance2));
            }

            public class Subject : ValueObject<Subject>
            {
                public string Value { get; set; }
            }

            private class BootStrapper : IBootstrap<Subject>
            {
                public void Bootstrap(IConfiguration configure)
                {
                    configure.ValueObject<Subject>().ToUseEqualityComparer(new EqualityComparer());
                }
            }

            private class EqualityComparer : IEqualityComparer<Subject>
            {
                public bool Equals(Subject x, Subject y)
                {
                    return x.Value.Equals(y.Value, StringComparison.OrdinalIgnoreCase);
                }

                public int GetHashCode(Subject obj)
                {
                    return obj.Value.GetHashCode();
                }
            }
        }
    }
}
