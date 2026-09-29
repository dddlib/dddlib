namespace dddlib.Tests.Bug;

// https://github.com/dddlib/dddlib/issues/128
// Legacy: the default value object equality comparer threw at construction for a value object with no public
// properties, unless a comparer was configured in the bootstrapper, and that comparer had to be resolved lazily.
// In v2 value objects are records. A record with only private fields constructs fine and compares by those fields,
// and custom comparison is an Equals override on the record, so there is nothing to resolve lazily.
public class Bug0128
{
    [Test]
    public async Task PrivateFieldsParticipateInEquality()
    {
        var a = new Subject("test");
        var b = new Subject("test");
        var c = new Subject("other");

        await Assert.That(a).IsEqualTo(b);
        await Assert.That(a).IsNotEqualTo(c);
    }

    [Test]
    public async Task ShouldNotThrow()
    {
        var action = () => { _ = new OtherSubject("test"); };

        await Assert.That(action).ThrowsNothing();
    }

    [Test]
    public async Task AreEqual()
    {
        var a = new OtherSubject("test");
        var b = new OtherSubject("TEST");

        await Assert.That(a).IsEqualTo(b);
    }

    public sealed record Subject
    {
        private readonly string value;

        public Subject(string value)
        {
            this.value = value;
        }

        public override string ToString() => this.value;
    }

    public sealed record OtherSubject
    {
        private readonly string value;

        public OtherSubject(string value)
        {
            this.value = value;
        }

        public bool Equals(OtherSubject? other) =>
            other is not null && string.Equals(this.value, other.value, StringComparison.OrdinalIgnoreCase);

        public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(this.value);

        public override string ToString() => this.value;
    }
}
