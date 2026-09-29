using dddlib.Configuration;
using dddlib.Tests.Support;

namespace dddlib.Tests.Features.Generated;

// As someone who uses dddlib
// In order to compare value objects
// I need value objects to have structural equality over their public properties
public abstract partial class ValueObjectEquality : Feature
{
    public sealed partial class UndefinedEqualityComparer : ValueObjectEquality
    {
        [Test]
        public async Task Scenario()
        {
            // Given a value object with an undefined equality comparer
            // And a value
            var value = "key";

            // When two instances of that value object are instantiated with the same value
            var instance1 = new Subject { Value = value };
            var instance2 = new Subject { Value = value };

            // Then the first instance is equal to the second instance
            await Assert.That(instance1).IsEqualTo(instance2);
            await Assert.That(instance1 == instance2).IsTrue();
            await Assert.That(instance1.GetHashCode()).IsEqualTo(instance2.GetHashCode());
        }

        public partial class Subject : ValueObject<Subject>
        {
            public string? Value { get; set; }
        }
    }

    public sealed partial class EqualityComparerDefinedInBootstrapper : ValueObjectEquality
    {
        [Test]
        public async Task Scenario()
        {
            // Given a value object with an equality comparer defined in the bootstrapper
            // When two instances of that value object are instantiated with the 'same' value
            var instance1 = new Subject { Value = "a" };
            var instance2 = new Subject { Value = "b" };

            // Then the first instance is equal to the second instance
            await Assert.That(instance1).IsEqualTo(instance2);
        }

        public partial class Subject : ValueObject<Subject>
        {
            public string? Value { get; set; }
        }

        private sealed partial class BootStrapper : IBootstrap<Subject>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.ValueObject<Subject>().ToUseEqualityComparer(new EqualityComparer());
            }
        }

        private sealed partial class EqualityComparer : IEqualityComparer<Subject>
        {
            public bool Equals(Subject? x, Subject? y) => x?.Value == "a" && y?.Value == "b";

            public int GetHashCode(Subject obj) => 0;
        }
    }

    public sealed partial class CaseSensitiveUndefinedEqualityComparer : ValueObjectEquality
    {
        [Test]
        public async Task Scenario()
        {
            // Given a value object with an undefined equality comparer
            // When two instances of that value object are instantiated with values that differ by case
            var instance1 = new Subject { Value = "CASE" };
            var instance2 = new Subject { Value = "case" };

            // Then the first instance is not equal to the second instance
            await Assert.That(instance1).IsNotEqualTo(instance2);
            await Assert.That(instance1 != instance2).IsTrue();
        }

        public partial class Subject : ValueObject<Subject>
        {
            public string? Value { get; set; }
        }
    }

    public sealed partial class CaseInsensitiveStringEqualityComparerDefinedInBootstrapper : ValueObjectEquality
    {
        [Test]
        public async Task Scenario()
        {
            // Given a value object with a case-insensitive equality comparer defined in the bootstrapper
            // When two instances of that value object are instantiated with values that differ by case
            var instance1 = new Subject { Value = "CASE" };
            var instance2 = new Subject { Value = "case" };

            // Then the first instance is equal to the second instance
            await Assert.That(instance1).IsEqualTo(instance2);
            await Assert.That(instance1.GetHashCode()).IsEqualTo(instance2.GetHashCode());
        }

        public partial class Subject : ValueObject<Subject>
        {
            public string? Value { get; set; }
        }

        private sealed partial class BootStrapper : IBootstrap<Subject>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.ValueObject<Subject>().ToUseEqualityComparer(new EqualityComparer());
            }
        }

        private sealed partial class EqualityComparer : IEqualityComparer<Subject>
        {
            public bool Equals(Subject? x, Subject? y) => string.Equals(x?.Value, y?.Value, StringComparison.OrdinalIgnoreCase);

            public int GetHashCode(Subject obj) => StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Value ?? string.Empty);
        }
    }

    public sealed partial class CollectionMemberComparesBySequence : ValueObjectEquality
    {
        [Test]
        public async Task Scenario()
        {
            // Given a value object with a collection-typed member and an undefined equality comparer
            // When two instances of that value object are instantiated with distinct but sequence-equal collections
            var instance1 = new Subject { Elements = new List<string> { "hello", "world" } };
            var instance2 = new Subject { Elements = new[] { "hello", "world" } };
            var instance3 = new Subject { Elements = new[] { "world", "hello" } };

            // Then the first instance is equal to the second instance but not the third
            await Assert.That(instance1).IsEqualTo(instance2);
            await Assert.That(instance1.GetHashCode()).IsEqualTo(instance2.GetHashCode());
            await Assert.That(instance1).IsNotEqualTo(instance3);
        }

        public partial class Subject : ValueObject<Subject>
        {
            public IEnumerable<string>? Elements { get; set; }
        }
    }

    public sealed partial class PrivateFieldsDoNotParticipateInEquality : ValueObjectEquality
    {
        [Test]
        public async Task Scenario()
        {
            // Given a value object with a public property and a private cache field
            // When two instances of that value object are instantiated with the same value but different cache state
            var instance1 = new Subject("key");
            var instance2 = new Subject("key");
            _ = instance1.Length;

            // Then the first instance is equal to the second instance
            await Assert.That(instance1).IsEqualTo(instance2);
            await Assert.That(instance1.GetHashCode()).IsEqualTo(instance2.GetHashCode());
        }

        public partial class Subject : ValueObject<Subject>
        {
            private int? cachedLength;

            public Subject(string value)
            {
                this.Value = value;
            }

            public string Value { get; }

            public int Length => this.cachedLength ??= this.Value.Length;
        }
    }
}
