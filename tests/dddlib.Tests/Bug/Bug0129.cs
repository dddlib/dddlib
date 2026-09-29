namespace dddlib.Tests.Bug;

// https://github.com/dddlib/dddlib/issues/129
// Legacy: the default value object equality comparer compared IEnumerable members by sequence.
// In v2 value objects are records, which compare collection members by reference, so sequence equality is an
// Equals override on the record (see ValueObjectEquality.CollectionMemberComparesBySequenceViaEqualsOverride).
public class Bug0129
{
    [Test]
    public async Task ShouldNotThrow()
    {
        var action = () => { _ = new Subject(); };

        await Assert.That(action).ThrowsNothing();
    }

    [Test]
    public async Task ShouldBeEqual()
    {
        var a = new Subject { Elements = new[] { "hello" } };
        var b = new Subject { Elements = new List<string> { "hello" } };

        await Assert.That(a).IsEqualTo(b);
    }

    public sealed record Subject
    {
        public IEnumerable<string>? Elements { get; init; }

        public bool Equals(Subject? other) =>
            other is not null &&
            (this.Elements is null ? other.Elements is null : other.Elements is not null && this.Elements.SequenceEqual(other.Elements));

        public override int GetHashCode()
        {
            var hash = default(HashCode);
            foreach (var element in this.Elements ?? [])
            {
                hash.Add(element, StringComparer.Ordinal);
            }

            return hash.ToHashCode();
        }
    }
}
