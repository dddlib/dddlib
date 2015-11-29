// <copyright file="DefaultIdentityMap.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Sdk
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using dddlib.Runtime;

    /// <summary>
    /// Represents the default identity map.
    /// </summary>
    public class DefaultIdentityMap : IIdentityMap
    {
        private readonly ConcurrentDictionary<Type, ConcurrentDictionary<object, Guid>> store =
            new ConcurrentDictionary<Type, ConcurrentDictionary<object, Guid>>();

        private readonly INaturalKeyRepository repository;
        private readonly INaturalKeySerializer serializer;

        private long checkpoint;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultIdentityMap"/> class.
        /// </summary>
        /// <param name="repository">The repository.</param>
        /// <param name="serializer">The serializer.</param>
        public DefaultIdentityMap(INaturalKeyRepository repository, INaturalKeySerializer serializer)
        {
            Guard.Against.Null(() => repository);
            Guard.Against.Null(() => serializer);

            this.repository = repository;
            this.serializer = serializer;
        }

        /// <summary>
        /// Gets the mapped identity for the specified natural key. If a mapping does not exist then one is created.
        /// </summary>
        /// <param name="aggregateRootType">Type of the aggregate root.</param>
        /// <param name="naturalKeyType">Type of the natural key.</param>
        /// <param name="naturalKey">The natural key.</param>
        /// <returns>The mapped identity.</returns>
        public Guid GetOrAdd(Type aggregateRootType, Type naturalKeyType, object naturalKey)
        {
            Guard.Against.Null(() => aggregateRootType);

            this.Validate(aggregateRootType, naturalKeyType, naturalKey);

            var mappings = this.store.GetOrAdd(aggregateRootType, _ => new ConcurrentDictionary<object, Guid>());

            var identity = default(Guid);
            while (!this.TryGet(aggregateRootType, naturalKeyType, naturalKey, out identity))
            {
                var naturalKeyRecord = default(NaturalKeyRecord);
                if (this.repository.TryAddNaturalKey(
                    aggregateRootType, 
                    this.serializer.Serialize(naturalKeyType, naturalKey), 
                    this.checkpoint, 
                    out naturalKeyRecord))
                {
                    // TODO (Cameron): Confirm that this is what we want to do here.
                    mappings.TryAdd(naturalKey, naturalKeyRecord.Identity);
                    return naturalKeyRecord.Identity;
                }
            }

            return identity;
        }

        /// <summary>
        /// Attempts to get the mapped identity for the specified natural key.
        /// </summary>
        /// <param name="aggregateRootType">Type of the aggregate root.</param>
        /// <param name="naturalKeyType">Type of the natural key.</param>
        /// <param name="naturalKey">The natural key.</param>
        /// <param name="identity">The mapped identity.</param>
        /// <returns>Returns <c>true</c> if the mapping exists; otherwise <c>false</c>.</returns>
        public bool TryGet(Type aggregateRootType, Type naturalKeyType, object naturalKey, out Guid identity)
        {
            Guard.Against.Null(() => aggregateRootType);

            this.Validate(aggregateRootType, naturalKeyType, naturalKey);

            var mappings = this.store.GetOrAdd(aggregateRootType, _ => new ConcurrentDictionary<object, Guid>());

            if (mappings.TryGetValue(naturalKey, out identity))
            {
                return true;
            }

            while (this.Synchronize(aggregateRootType, naturalKeyType, mappings))
            {
                if (mappings.TryGetValue(naturalKey, out identity))
                {
                    return true;
                }
            }

            return false;
        }

        private bool Synchronize(Type aggregateRootType, Type naturalKeyType, ConcurrentDictionary<object, Guid> mappings)
        {
            var startCheckpoint = this.checkpoint;
            foreach (var naturalKeyRecord in this.repository.GetNaturalKeys(aggregateRootType, startCheckpoint))
            {
                var naturalKey = this.serializer.Deserialize(naturalKeyType, naturalKeyRecord.SerializedValue);

                // TODO (Cameron): Verify that no if clause is required here.
                mappings.TryAdd(naturalKey, naturalKeyRecord.Identity);

                this.checkpoint = naturalKeyRecord.Checkpoint;
            }

            return this.checkpoint != startCheckpoint;
        }

        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1118:ParameterMustNotSpanMultipleLines", Justification = "It's fine here.")]
        private void Validate(Type aggregateRootType, Type naturalKeyType, object naturalKey)
        {
            if (this.store.ContainsKey(aggregateRootType))
            {
                // NOTE (Cameron): We must have already performed validation for this aggregate root type.
                return;
            }

            var serializedNaturalKey = this.serializer.Serialize(naturalKeyType, naturalKey);
            var deserializedNaturalKey = this.serializer.Deserialize(naturalKeyType, serializedNaturalKey);

            if (object.Equals(naturalKey, deserializedNaturalKey))
            {
                return;
            }

            throw new RuntimeException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    @"The natural key of type '{0}' defined for aggregate root of type '{1}' does not meet equality expectations following identity map serialization.
Check that the natural key is correctly defined, implements value object equality, and can be successfully serialized and deserialized.",
                    naturalKeyType,
                    aggregateRootType));
        }
    }
}
