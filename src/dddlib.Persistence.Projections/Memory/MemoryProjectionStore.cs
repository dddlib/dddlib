using System.Globalization;
using dddlib.Persistence.Projections.Sdk;
using dddlib.Persistence.Sdk;

namespace dddlib.Persistence.Projections.Memory;

/// <summary>
/// Runs a key/value projection against a <see cref="MemoryRepository{TIdentity, TEntity}"/>, with the checkpoint in
/// memory. Handlers write to a buffer; the buffer and the checkpoint are committed together under a lock, so a page
/// that fails changes nothing and two runners never both apply one.
/// </summary>
public sealed class MemoryProjectionStore<TIdentity, TEntity> : IProjectionStore
    where TIdentity : notnull
    where TEntity : class
{
    private readonly Lock sync = new();
    private readonly Projection<TIdentity, TEntity> projection;
    private long checkpoint;

    public MemoryProjectionStore(Projection<TIdentity, TEntity> projection, MemoryRepository<TIdentity, TEntity>? views = null)
    {
        ArgumentNullException.ThrowIfNull(projection);

        this.projection = projection;
        this.Views = views ?? new MemoryRepository<TIdentity, TEntity>();
    }

    public string Name => this.projection.Name;

    public IReadOnlyCollection<Type>? EventTypes => this.projection.EventTypes;

    /// <summary>
    /// Gets the views the projection writes, for readers.
    /// </summary>
    public MemoryRepository<TIdentity, TEntity> Views { get; }

    public Task<long> GetCheckpointAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            return Task.FromResult(this.checkpoint);
        }
    }

    public async Task ApplyAsync(EventPage page, long expectedCheckpoint, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);

        var buffer = new BufferedRepository<TIdentity, TEntity>(this.Views, this.Views.Comparer);
        foreach (var @event in page.Events)
        {
            await this.projection.ApplyAsync(@event, buffer, cancellationToken).ConfigureAwait(false);
        }

        lock (this.sync)
        {
            if (this.checkpoint != expectedCheckpoint)
            {
                throw new ConcurrencyException(
                    string.Format(CultureInfo.InvariantCulture, "The checkpoint of the projection '{0}' is {1}, not {2}.", this.Name, this.checkpoint, expectedCheckpoint));
            }

            this.Views.Apply(buffer.Changes, buffer.IsPurged);
            this.checkpoint = page.EndSequenceNumber;
        }
    }

    public Task PurgeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            this.Views.Apply([], purge: true);
            this.checkpoint = 0;
        }

        return Task.CompletedTask;
    }
}
