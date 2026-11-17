using System.Globalization;
using dddlib.Persistence.Sdk;

namespace dddlib.Persistence.Memory;

/// <summary>
/// A batch of aggregate roots saved through <see cref="MemoryRepository{T}"/> instances, of any aggregate root types,
/// in one transaction: every memento is stored and every event appended, or nothing is.
/// </summary>
public sealed class MemoryBatch : IBatch
{
    private readonly BatchSaves<IMemoryBatchParticipant> saves = new();

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var entries = this.saves.Start();

        await BatchSaves<IMemoryBatchParticipant>.SaveAsync(entries, static entries => Task.FromResult<IReadOnlyList<string>>(Write(entries)), cancellationToken).ConfigureAwait(false);
    }

    internal static string NewState() => Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

    internal void Add(BatchSave save, IMemoryBatchParticipant participant) => this.saves.Add(save, participant);

    private static string[] Write(IReadOnlyList<(BatchSave Save, IMemoryBatchParticipant Participant)> entries)
    {
        // Everything that can fail without touching the stores is done first.
        var mementos = entries.Select(static entry => entry.Participant.Stage(entry.Save.Memento)).ToArray();
        var events = entries.Select(static entry => MemoryEventStore.Stage(entry.Save.Events)).ToArray();

        // The locks of a single save, in a fixed order so that batches cannot deadlock each other: every repository's,
        // then every event store's.
        var participants = entries.Select(static entry => entry.Participant).Distinct().OrderBy(static participant => participant.BatchOrder).ToArray();
        var locks = participants.Select(static participant => participant.Sync)
            .Concat(participants.Select(static participant => participant.EventStore).Distinct().OrderBy(static store => store.BatchOrder).Select(static store => store.Sync))
            .ToArray();

        var entered = 0;
        try
        {
            foreach (var @lock in locks)
            {
                @lock.Enter();
                entered++;
            }

            foreach (var (save, participant) in entries)
            {
                var conflict = participant.FindConflict(save.Id, save.PreCommitState);
                if (conflict is not null)
                {
                    throw save.Conflict(conflict);
                }
            }

            var correlationId = Guid.NewGuid();
            var states = new string[entries.Count];
            for (var index = 0; index < entries.Count; index++)
            {
                var (save, participant) = entries[index];
                states[index] = NewState();
                participant.EventStore.AppendStaged(save.Id, events[index], correlationId, states[index]);
                participant.Store(save.Id, mementos[index], states[index]);
            }

            return states;
        }
        finally
        {
            while (entered > 0)
            {
                locks[--entered].Exit();
            }
        }
    }
}
