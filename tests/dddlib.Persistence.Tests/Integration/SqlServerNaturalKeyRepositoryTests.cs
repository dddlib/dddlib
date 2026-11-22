using dddlib.Persistence.SqlServer;
using dddlib.Sdk;
using dddlib.Tests.Support;
using Microsoft.Data.SqlClient;

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

    // Every write of a type's natural keys, through either protocol, waits its turn on the type's checkpoint lock, and
    // gives up with a concurrency error rather than at the client's command timeout
    // (https://github.com/dddlib/dddlib/issues/57).
    [Test]
    public async Task EveryWriteOfATypesNaturalKeysGivesUpClearlyWhenTheTypeStaysLocked()
    {
        var repository = new SqlServerNaturalKeyRepository(this.ConnectionString);
        var existing = await repository.GetOrAddNaturalKeyAsync(typeof(LockedSubject), "\"existing\"");

        // another writer holds the type's checkpoint lock, as it does while it takes a checkpoint
        await using var connection = new SqlConnection(this.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                """
                DECLARE @Resource NVARCHAR(255) = (
                    SELECT CONCAT(N'dddlib.NaturalKeys.', OBJECT_ID(N'[dbo].[NaturalKeys]'), N'.', [Id])
                    FROM [dbo].[Types]
                    WHERE [Name] = @Name);
                EXEC sp_getapplock @Resource = @Resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction';
                """;
            command.Parameters.AddWithValue("@Name", typeof(LockedSubject).GetSerializedName());
            await command.ExecuteNonQueryAsync();
        }

        var getOrAdd = repository.GetOrAddNaturalKeyAsync(typeof(LockedSubject), "\"new\"");
        var tryAdd = repository.TryAddNaturalKeyAsync(typeof(LockedSubject), "\"other\"", existing.Checkpoint);
        var remove = repository.RemoveAsync(existing.Identity);

        await Assert.That(() => (Task)getOrAdd).Throws<ConcurrencyException>().WithMessageContaining("natural key checkpoint lock");
        await Assert.That(() => (Task)tryAdd).Throws<ConcurrencyException>().WithMessageContaining("natural key checkpoint lock");
        await Assert.That(() => (Task)remove).Throws<ConcurrencyException>().WithMessageContaining("natural key checkpoint lock");
    }

    private class Subject : AggregateRoot
    {
    }

    private class LockedSubject : AggregateRoot
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
