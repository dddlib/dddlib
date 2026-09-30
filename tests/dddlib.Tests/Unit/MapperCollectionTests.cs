using dddlib.Sdk;

namespace dddlib.Tests.Unit;

public class MapperCollectionTests
{
    [Test]
    public async Task KeepsMappingsOfEachShapeApart()
    {
        var mappings = new MapperCollection();
        Func<string, Target> create = static source => new Target(source);
        Func<string, Target, Target> copy = static (source, target) => target with { Value = source };
        Func<Target, string> reverse = static target => target.Value;

        mappings.AddOrUpdate(create);
        mappings.AddOrUpdate(copy);
        mappings.AddOrUpdate(reverse);

        await Assert.That(mappings.TryGet<string, Target>(out Func<string, Target>? foundCreate)).IsTrue();
        await Assert.That(mappings.TryGet<string, Target>(out Func<string, Target, Target>? foundCopy)).IsTrue();
        await Assert.That(mappings.TryGet<Target, string>(out Func<Target, string>? foundReverse)).IsTrue();
        await Assert.That(foundCreate).IsSameReferenceAs(create);
        await Assert.That(foundCopy).IsSameReferenceAs(copy);
        await Assert.That(foundReverse).IsSameReferenceAs(reverse);
        await Assert.That(mappings.TryGet<string, Target>(out Action<string, Target>? _)).IsFalse();
        await Assert.That(mappings.TryGet<int, Target>(out Func<int, Target>? _)).IsFalse();
    }

    [Test]
    public async Task TheMappingToAnExistingDestinationIsTheOneConfiguredLast()
    {
        var mappings = new MapperCollection();
        Action<string, Target> change = static (source, target) => { };
        Func<string, Target, Target> copy = static (source, target) => target with { Value = source };

        mappings.AddOrUpdate(change);
        mappings.AddOrUpdate(copy);

        await Assert.That(mappings.TryGet<string, Target>(out Action<string, Target>? _)).IsFalse();
        await Assert.That(mappings.TryGet<string, Target>(out Func<string, Target, Target>? _)).IsTrue();

        mappings.AddOrUpdate(change);

        await Assert.That(mappings.TryGet<string, Target>(out Action<string, Target>? _)).IsTrue();
        await Assert.That(mappings.TryGet<string, Target>(out Func<string, Target, Target>? _)).IsFalse();
    }

    private sealed record Target(string Value);
}
