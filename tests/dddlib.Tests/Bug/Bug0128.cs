using dddlib.Runtime;

namespace dddlib.Tests.Bug;

// https://github.com/dddlib/dddlib/issues/128
// A value object with no public properties fails at construction under the default equality comparer, unless a
// comparer is configured in the bootstrapper. The default comparer must therefore be created lazily, after the
// bootstrapper has had its chance.
public class Bug0128
{
    [Test]
    public async Task ShouldThrow()
    {
        var action = () => { _ = new Subject("test"); };

        await Assert.That(action).Throws<RuntimeException>();
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

    public sealed class Subject : ValueObject<Subject>
    {
        private readonly string value;

        public Subject(string value)
        {
            this.value = value;
        }

        public override string ToString() => this.value;
    }

    public sealed class OtherSubject : ValueObject<OtherSubject>
    {
        private readonly string value;

        public OtherSubject(string value)
        {
            this.value = value;
        }

        public override string ToString() => this.value;

        internal sealed class EqualityComparer : IEqualityComparer<OtherSubject>
        {
            public bool Equals(OtherSubject? x, OtherSubject? y) =>
                x is null || y is null ? x is null && y is null : string.Equals(x.value, y.value, StringComparison.OrdinalIgnoreCase);

            public int GetHashCode(OtherSubject obj) => StringComparer.OrdinalIgnoreCase.GetHashCode(obj.value);
        }
    }
}
