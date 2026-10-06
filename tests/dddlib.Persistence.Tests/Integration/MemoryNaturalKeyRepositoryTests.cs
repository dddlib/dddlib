using dddlib.Persistence.Memory;

namespace dddlib.Persistence.Tests.Integration;

public class MemoryNaturalKeyRepositoryTests
{
    [Test]
    public Task GetOrAddFollowsTheUniqueKeyContract() =>
        UniqueNaturalKeyRepositoryContract.VerifyAsync(new MemoryNaturalKeyRepository(), typeof(Subject), typeof(OtherSubject));

    private class Subject : AggregateRoot
    {
    }

    private class OtherSubject : AggregateRoot
    {
    }
}
