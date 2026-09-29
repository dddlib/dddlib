using System.Collections.Concurrent;
using System.Globalization;
using dddlib.Runtime;

namespace dddlib.Persistence.Sdk;

/// <summary>
/// An identity map that caches natural key to identity mappings in memory and keeps them in sync with an
/// <see cref="INaturalKeyRepository"/> through its checkpoints.
/// </summary>
public class DefaultIdentityMap : IIdentityMap
{
    private const int MaxAddAttempts = 100;

    private readonly ConcurrentDictionary<Type, TypeMap> maps = new();
    private readonly INaturalKeyRepository repository;
    private readonly INaturalKeySerializer serializer;

    public DefaultIdentityMap(INaturalKeyRepository repository, INaturalKeySerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(serializer);

        this.repository = repository;
        this.serializer = serializer;
    }

    public async Task<Guid> GetOrAddAsync(Type aggregateRootType, Type naturalKeyType, object naturalKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aggregateRootType);
        ArgumentNullException.ThrowIfNull(naturalKeyType);
        ArgumentNullException.ThrowIfNull(naturalKey);

        var map = this.GetMap(aggregateRootType, naturalKeyType, naturalKey);

        for (var attempt = 0; attempt < MaxAddAttempts; attempt++)
        {
            await this.SynchronizeAsync(aggregateRootType, naturalKeyType, map, cancellationToken).ConfigureAwait(false);
            if (map.Mappings.TryGetValue(naturalKey, out var existing))
            {
                return existing;
            }

            var record = await this.repository
                .TryAddNaturalKeyAsync(aggregateRootType, this.serializer.Serialize(naturalKeyType, naturalKey), map.Checkpoint, cancellationToken)
                .ConfigureAwait(false);

            if (record is not null)
            {
                map.Mappings.TryAdd(naturalKey, record.Identity);
                return record.Identity;
            }
        }

        throw new PersistenceException(
            string.Format(CultureInfo.InvariantCulture, "Unable to add a natural key for aggregate root of type '{0}' after {1} attempts.", aggregateRootType, MaxAddAttempts));
    }

    public async Task<Guid?> TryGetAsync(Type aggregateRootType, Type naturalKeyType, object naturalKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aggregateRootType);
        ArgumentNullException.ThrowIfNull(naturalKeyType);
        ArgumentNullException.ThrowIfNull(naturalKey);

        var map = this.GetMap(aggregateRootType, naturalKeyType, naturalKey);
        await this.SynchronizeAsync(aggregateRootType, naturalKeyType, map, cancellationToken).ConfigureAwait(false);

        return map.Mappings.TryGetValue(naturalKey, out var identity) ? identity : null;
    }

    public Task RemoveAsync(Guid identity, CancellationToken cancellationToken = default) =>
        this.repository.RemoveAsync(identity, cancellationToken);

    private TypeMap GetMap(Type aggregateRootType, Type naturalKeyType, object naturalKey)
    {
        if (this.maps.TryGetValue(aggregateRootType, out var map))
        {
            return map;
        }

        this.ValidateSerialization(aggregateRootType, naturalKeyType, naturalKey);
        return this.maps.GetOrAdd(aggregateRootType, static _ => new TypeMap());
    }

    private async Task SynchronizeAsync(Type aggregateRootType, Type naturalKeyType, TypeMap map, CancellationToken cancellationToken)
    {
        await map.Sync.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var records = await this.repository.GetNaturalKeysAsync(aggregateRootType, map.Checkpoint, cancellationToken).ConfigureAwait(false);
            foreach (var record in records)
            {
                var naturalKey = this.serializer.Deserialize(naturalKeyType, record.SerializedValue);
                if (record.IsRemoved)
                {
                    map.Mappings.TryRemove(naturalKey, out _);
                }
                else
                {
                    map.Mappings[naturalKey] = record.Identity;
                }

                map.Checkpoint = record.Checkpoint;
            }
        }
        finally
        {
            map.Sync.Release();
        }
    }

    private void ValidateSerialization(Type aggregateRootType, Type naturalKeyType, object naturalKey)
    {
        var serializedNaturalKey = this.serializer.Serialize(naturalKeyType, naturalKey);
        var deserializedNaturalKey = this.serializer.Deserialize(naturalKeyType, serializedNaturalKey);
        if (object.Equals(naturalKey, deserializedNaturalKey))
        {
            return;
        }

        throw new RuntimeException(
            string.Format(
                CultureInfo.InvariantCulture,
                @"The natural key of type '{0}' defined for aggregate root of type '{1}' does not meet equality expectations following serialization.
To fix this issue, check that the natural key:
- is correctly defined in either a bootstrapper or through use of the [dddlib.NaturalKey] attribute, and
- implements value object equality, and
- can be successfully serialized and deserialized.",
                naturalKeyType,
                aggregateRootType))
        {
            HelpLink = "https://github.com/dddlib/dddlib/wiki/Value-Object-Serialization",
        };
    }

    private sealed class TypeMap
    {
        public ConcurrentDictionary<object, Guid> Mappings { get; } = new();

        public SemaphoreSlim Sync { get; } = new(1, 1);

        public long Checkpoint { get; set; }
    }
}
