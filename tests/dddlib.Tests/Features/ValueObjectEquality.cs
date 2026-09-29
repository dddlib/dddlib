namespace dddlib.Tests.Features;

// As someone who uses dddlib
// In order to compare value objects
// I need value objects to have structural equality
//
// In v2 value objects are C# records. These scenarios re-express the legacy ValueObjectEquality feature
// to document what records provide natively and where the user has to step in (custom comparison and
// collection-typed members). See PLAN.md section 4.
public abstract class ValueObjectEquality
{
    public sealed class UndefinedEqualityComparer : ValueObjectEquality
    {
        [Test]
        public async Task Scenario()
        {
            // Given a value object record with no custom equality
            // And a value
            var value = "key";

            // When two instances of that value object are instantiated with the same value
            var instance1 = new Subject(value);
            var instance2 = new Subject(value);

            // Then the first instance is equal to the second instance
            await Assert.That(instance1).IsEqualTo(instance2);
            await Assert.That(instance1 == instance2).IsTrue();
        }

        public sealed record Subject(string Value);
    }

    public sealed class CaseSensitiveUndefinedEqualityComparer : ValueObjectEquality
    {
        [Test]
        public async Task Scenario()
        {
            // Given a value object record with no custom equality
            // When two instances of that value object are instantiated with values that differ by case
            var instance1 = new Subject("CASE");
            var instance2 = new Subject("case");

            // Then the first instance is not equal to the second instance
            await Assert.That(instance1).IsNotEqualTo(instance2);
            await Assert.That(instance1 != instance2).IsTrue();
        }

        public sealed record Subject(string Value);
    }

    public sealed class CaseInsensitiveEqualityViaEqualsOverride : ValueObjectEquality
    {
        [Test]
        public async Task Scenario()
        {
            // Given a value object record that overrides Equals to compare case-insensitively
            // When two instances of that value object are instantiated with values that differ by case
            var instance1 = new Subject("CASE");
            var instance2 = new Subject("case");

            // Then the first instance is equal to the second instance
            await Assert.That(instance1).IsEqualTo(instance2);
            await Assert.That(instance1 == instance2).IsTrue();
            await Assert.That(instance1.GetHashCode()).IsEqualTo(instance2.GetHashCode());
        }

        public sealed record Subject(string Value)
        {
            public bool Equals(Subject? other) =>
                other is not null && string.Equals(this.Value, other.Value, StringComparison.OrdinalIgnoreCase);

            public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(this.Value);
        }
    }

    public sealed class CollectionMemberComparesByReference : ValueObjectEquality
    {
        [Test]
        public async Task Scenario()
        {
            // Given a value object record with a collection-typed member and no custom equality
            // When two instances of that value object are instantiated with distinct but sequence-equal collections
            var instance1 = new Subject(new List<string> { "hello" });
            var instance2 = new Subject(new List<string> { "hello" });

            // Then the first instance is not equal to the second instance (records compare the member by reference)
            await Assert.That(instance1).IsNotEqualTo(instance2);
        }

        public sealed record Subject(List<string> Elements);
    }

    public sealed class CollectionMemberComparesBySequenceViaEqualsOverride : ValueObjectEquality
    {
        [Test]
        public async Task Scenario()
        {
            // Given a value object record with a collection-typed member that overrides Equals to use sequence equality
            // When two instances of that value object are instantiated with distinct but sequence-equal collections
            var instance1 = new Subject(new List<string> { "hello" });
            var instance2 = new Subject(new List<string> { "hello" });

            // Then the first instance is equal to the second instance
            await Assert.That(instance1).IsEqualTo(instance2);
            await Assert.That(instance1.GetHashCode()).IsEqualTo(instance2.GetHashCode());
        }

        public sealed record Subject(List<string> Elements)
        {
            public bool Equals(Subject? other) => other is not null && this.Elements.SequenceEqual(other.Elements);

            public override int GetHashCode()
            {
                var hash = default(HashCode);
                foreach (var element in this.Elements)
                {
                    hash.Add(element, StringComparer.Ordinal);
                }

                return hash.ToHashCode();
            }
        }
    }
}
