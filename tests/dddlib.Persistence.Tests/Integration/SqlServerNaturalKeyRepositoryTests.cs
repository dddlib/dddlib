using dddlib.Persistence.SqlServer;
using dddlib.Tests.Support;

namespace dddlib.Persistence.Tests.Integration;

// The legacy file was commented out; this covers the checkpoint contract the identity map relies on.
public class SqlServerNaturalKeyRepositoryTests : SqlServerIntegration
{
    [Test]
    public async Task AddGetAndRemoveFollowTheCheckpointContract()
    {
        var repository = new SqlServerNaturalKeyRepository(this.ConnectionString);

        // an add at the latest checkpoint succeeds; an add at a stale checkpoint does not
        var added = await repository.TryAddNaturalKeyAsync(typeof(Subject), "\"key\"", 0);
        var stale = await repository.TryAddNaturalKeyAsync(typeof(Subject), "\"other\"", 0);
        var next = await repository.TryAddNaturalKeyAsync(typeof(Subject), "\"other\"", added!.Checkpoint);

        await Assert.That(added).IsNotNull();
        await Assert.That(stale).IsNull();
        await Assert.That(next).IsNotNull();
        await Assert.That(next!.Checkpoint).IsGreaterThan(added.Checkpoint);

        // records are returned after a checkpoint, in checkpoint order
        var all = await repository.GetNaturalKeysAsync(typeof(Subject), 0);
        var later = await repository.GetNaturalKeysAsync(typeof(Subject), added.Checkpoint);

        await Assert.That(all.Select(record => record.Identity)).IsEquivalentTo([added.Identity, next.Identity]);
        await Assert.That(later.Select(record => record.Identity)).IsEquivalentTo([next.Identity]);

        // a removal surfaces as a removed record at a later checkpoint
        await repository.RemoveAsync(added.Identity);
        var afterRemoval = await repository.GetNaturalKeysAsync(typeof(Subject), next.Checkpoint);

        await Assert.That(afterRemoval).Count().IsEqualTo(1);
        await Assert.That(afterRemoval[0].Identity).IsEqualTo(added.Identity);
        await Assert.That(afterRemoval[0].IsRemoved).IsTrue();
    }

    [Test]
    public Task GetOrAddFollowsTheUniqueKeyContract() =>
        UniqueNaturalKeyRepositoryContract.VerifyAsync(new SqlServerNaturalKeyRepository(this.ConnectionString), typeof(UniqueSubject), typeof(OtherUniqueSubject));

    [Test]
    public async Task GetOrAddDoesNotTakeAKeyAddedThroughTheCheckpointProtocol()
    {
        var repository = new SqlServerNaturalKeyRepository(this.ConnectionString);

        var added = await repository.TryAddNaturalKeyAsync(typeof(ProtocolSubject), "\"key\"", 0);
        var found = await repository.GetOrAddNaturalKeyAsync(typeof(ProtocolSubject), "\"key\"");

        await Assert.That(found).IsEqualTo(added);
    }

    private class Subject : AggregateRoot
    {
    }

    // each test has types of its own: the tests share a database and run in parallel
    private class UniqueSubject : AggregateRoot
    {
    }

    private class OtherUniqueSubject : AggregateRoot
    {
    }

    private class ProtocolSubject : AggregateRoot
    {
    }
}
