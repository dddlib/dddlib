namespace dddlib.Tests.Bug;

// https://github.com/dddlib/dddlib/issues/129
// The default value object equality comparer compares IEnumerable members element by element, so an array and a
// list with the same elements are equal.
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
        await Assert.That(a.GetHashCode()).IsEqualTo(b.GetHashCode());
    }

    public class Subject : ValueObject<Subject>
    {
        public IEnumerable<string>? Elements { get; set; }
    }
}
