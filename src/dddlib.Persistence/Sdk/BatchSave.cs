using System.Globalization;
using dddlib.Sdk.Configuration.Model;

namespace dddlib.Persistence.Sdk;

/// <summary>
/// One aggregate root in a memento batch, created by its repository. The batch drives it through the parts of a save
/// that do not depend on the storage: <see cref="Prepare"/>, <see cref="ResolveIdentityAsync"/> and, once the storage
/// has committed, <see cref="Commit"/> and <see cref="CompleteAsync"/>.
/// </summary>
internal abstract class BatchSave(AggregateRoot aggregateRoot)
{
    public AggregateRoot AggregateRoot { get; } = aggregateRoot;

    public AggregateRootType RuntimeType { get; protected set; } = null!;

    public object NaturalKey { get; protected set; } = null!;

    public object Memento { get; protected set; } = null!;

    public IReadOnlyList<object> Events { get; protected set; } = [];

    public string? PreCommitState { get; protected set; }

    public Guid Id { get; protected set; }

    /// <summary>
    /// Validates the aggregate root and takes its memento and uncommitted events, without I/O.
    /// </summary>
    public abstract void Prepare();

    /// <summary>
    /// Resolves the identity the aggregate root is stored under.
    /// </summary>
    public abstract Task ResolveIdentityAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gives the aggregate root its new state token once every aggregate root of the batch is stored.
    /// </summary>
    public void Commit(string postCommitState) => this.AggregateRoot.CommitEvents(postCommitState);

    /// <summary>
    /// Finishes the save after the commit, for example by removing the identity of a destroyed aggregate root.
    /// </summary>
    public abstract Task CompleteAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Creates the exception for a conflict with the stored state of this aggregate root, naming its type and natural key.
    /// </summary>
    public ConcurrencyException Conflict(string reason, Exception? innerException = null) =>
        Conflict(this.RuntimeType, this.NaturalKey, reason, innerException);

    public static ConcurrencyException Conflict(AggregateRootType runtimeType, object naturalKey, string reason, Exception? innerException = null) =>
        new(
            string.Format(
                CultureInfo.InvariantCulture,
                "The aggregate root of type '{0}' with natural key '{1}' {2}.",
                runtimeType.RuntimeType,
                naturalKey,
                reason),
            innerException);
}
