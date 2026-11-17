namespace dddlib.Persistence.Memory;

/// <summary>
/// A memory repository as a <see cref="MemoryBatch"/> sees it, whatever its aggregate root type.
/// </summary>
internal interface IMemoryBatchParticipant
{
    /// <summary>
    /// Gets the lock that guards the repository's mementos.
    /// </summary>
    Lock Sync { get; }

    /// <summary>
    /// Gets the order in which a batch takes this repository's lock among the locks of other repositories.
    /// </summary>
    long BatchOrder { get; }

    MemoryEventStore EventStore { get; }

    /// <summary>
    /// Serializes a memento ahead of the commit, so that a failure to serialize it leaves the repository untouched.
    /// </summary>
    object Stage(object memento);

    /// <summary>
    /// Returns why storing a memento with the pre-commit state would conflict with the stored one (for example "already
    /// exists"), or null when it would not. The caller holds <see cref="Sync"/>.
    /// </summary>
    string? FindConflict(Guid id, string? preCommitState);

    /// <summary>
    /// Stores a staged memento with its new state token. The caller holds <see cref="Sync"/>.
    /// </summary>
    void Store(Guid id, object staged, string state);
}
