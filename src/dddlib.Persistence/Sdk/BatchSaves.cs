using dddlib.Runtime;

namespace dddlib.Persistence.Sdk;

/// <summary>
/// The aggregate roots added to a batch, each with the participant (its repository, as the batch sees it) that stores
/// it, and the parts of a batch save that every storage shares.
/// </summary>
internal sealed class BatchSaves<TParticipant>
{
    private readonly Lock sync = new();
    private readonly List<(BatchSave Save, TParticipant Participant)> entries = [];
    private readonly HashSet<AggregateRoot> aggregateRoots = new(ReferenceEqualityComparer.Instance);
    private bool started;

    public void Add(BatchSave save, TParticipant participant)
    {
        lock (this.sync)
        {
            if (this.started)
            {
                throw new InvalidOperationException("The batch has already been saved. Create a new batch for each save.");
            }

            if (!this.aggregateRoots.Add(save.AggregateRoot))
            {
                throw new ArgumentException("The aggregate root is already in the batch.");
            }

            this.entries.Add((save, participant));
        }
    }

    /// <summary>
    /// Marks the batch as used and returns its entries. A batch is saved once, whether or not the save succeeds.
    /// </summary>
    public IReadOnlyList<(BatchSave Save, TParticipant Participant)> Start()
    {
        lock (this.sync)
        {
            if (this.started)
            {
                throw new InvalidOperationException("The batch has already been saved. Create a new batch for each save.");
            }

            this.started = true;
            return [.. this.entries];
        }
    }

    /// <summary>
    /// Saves the entries: validates every aggregate root before anything is written, resolves the identities, has
    /// <paramref name="writeAsync"/> store everything in one transaction and return the new state tokens in entry
    /// order, then commits the aggregate roots and completes the saves.
    /// </summary>
    public static async Task SaveAsync(
        IReadOnlyList<(BatchSave Save, TParticipant Participant)> entries,
        Func<IReadOnlyList<(BatchSave Save, TParticipant Participant)>, Task<IReadOnlyList<string>>> writeAsync,
        CancellationToken cancellationToken)
    {
        if (entries.Count == 0)
        {
            return;
        }

        try
        {
            foreach (var (save, _) in entries)
            {
                save.Prepare();
            }

            // As for a single save, identities are resolved outside the write: a failed batch can leave a new aggregate
            // root's identity registered, and the next save of that natural key reuses it.
            var ids = new HashSet<Guid>();
            foreach (var (save, _) in entries)
            {
                await save.ResolveIdentityAsync(cancellationToken).ConfigureAwait(false);
                if (!ids.Add(save.Id))
                {
                    throw save.Conflict("appears more than once in the batch");
                }
            }

            var postCommitStates = await writeAsync(entries).ConfigureAwait(false);

            for (var index = 0; index < entries.Count; index++)
            {
                entries[index].Save.Commit(postCommitStates[index]);
            }

            foreach (var (save, _) in entries)
            {
                await save.CompleteAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (RuntimeException ex)
        {
            throw new PersistenceException(string.Concat("An exception occurred during the save operation.\r\n", ex.Message), ex);
        }
    }
}
