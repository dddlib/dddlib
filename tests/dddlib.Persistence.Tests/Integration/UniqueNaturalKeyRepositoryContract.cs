using dddlib.Persistence.Sdk;

namespace dddlib.Persistence.Tests.Integration;

/// <summary>
/// The contract of <see cref="IUniqueNaturalKeyRepository.GetOrAddNaturalKeyAsync"/>, shared by every repository.
/// </summary>
internal static class UniqueNaturalKeyRepositoryContract
{
    public static async Task VerifyAsync(IUniqueNaturalKeyRepository repository, Type aggregateRootType, Type otherAggregateRootType)
    {
        // the same serialized key gets the same identity; another key gets another identity at a later checkpoint
        var added = await repository.GetOrAddNaturalKeyAsync(aggregateRootType, "\"key\"");
        var again = await repository.GetOrAddNaturalKeyAsync(aggregateRootType, "\"key\"");
        var other = await repository.GetOrAddNaturalKeyAsync(aggregateRootType, "\"other\"");

        await Assert.That(again).IsEqualTo(added);
        await Assert.That(other.Identity).IsNotEqualTo(added.Identity);
        await Assert.That(other.Checkpoint).IsGreaterThan(added.Checkpoint);

        // keys are scoped to the aggregate root type
        var otherType = await repository.GetOrAddNaturalKeyAsync(otherAggregateRootType, "\"key\"");

        await Assert.That(otherType.Identity).IsNotEqualTo(added.Identity);

        // a removed key can be added again, as a new identity, and synchronizing sees the removal before the re-add
        await repository.RemoveAsync(added.Identity);
        var readded = await repository.GetOrAddNaturalKeyAsync(aggregateRootType, "\"key\"");
        var changes = await repository.GetNaturalKeysAsync(aggregateRootType, other.Checkpoint);

        await Assert.That(readded.Identity).IsNotEqualTo(added.Identity);
        await Assert.That(changes.Select(static record => (record.Identity, record.IsRemoved))).IsEquivalentTo([(added.Identity, true), (readded.Identity, false)]);
        await Assert.That(changes[0].Identity).IsEqualTo(added.Identity);

        // the checkpoint protocol sees keys added this way
        var stale = await repository.TryAddNaturalKeyAsync(aggregateRootType, "\"third\"", other.Checkpoint);

        await Assert.That(stale).IsNull();
    }
}
