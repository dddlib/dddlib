// <copyright file="MemoryNaturalKeyRepository.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Memory
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using dddlib.Persistence.Sdk;

    /// <summary>
    /// Represents the memory natural key repository.
    /// </summary>
    //// TODO (Cameron): I think this entire class is nonsense.
    public class MemoryNaturalKeyRepository : INaturalKeyRepository
    {
        private readonly ConcurrentDictionary<Type, ConcurrentDictionary<long, NaturalKeyRecord>> store =
            new ConcurrentDictionary<Type, ConcurrentDictionary<long, NaturalKeyRecord>>();

        private long lastCheckpoint;

        /// <summary>
        /// Gets the natural key records for the specified type of aggregate root from the specified checkpoint.
        /// </summary>
        /// <param name="aggregateRootType">Type of the aggregate root.</param>
        /// <param name="checkpoint">The checkpoint.</param>
        /// <returns>The natural key records.</returns>
        public IEnumerable<NaturalKeyRecord> GetNaturalKeys(Type aggregateRootType, long checkpoint)
        {
            var naturalKeyRecords = default(ConcurrentDictionary<long, NaturalKeyRecord>);
            return this.store.TryGetValue(aggregateRootType, out naturalKeyRecords)
                ? naturalKeyRecords.Where(kvp => kvp.Key > checkpoint).Select(kvp => kvp.Value).OrderBy(record => record.Checkpoint).ToArray()
                : Enumerable.Empty<NaturalKeyRecord>();
        }

        /// <summary>
        /// Attempts to add the natural key to the natural key records.
        /// </summary>
        /// <param name="aggregateRootType">Type of the aggregate root.</param>
        /// <param name="serializedNaturalKey">The serialized natural key.</param>
        /// <param name="checkpoint">The checkpoint.</param>
        /// <param name="naturalKeyRecord">The natural key record.</param>
        /// <returns>Returns <c>true</c> if the natural key record was successfully added; otherwise <c>false</c>.</returns>
        public bool TryAddNaturalKey(Type aggregateRootType, object serializedNaturalKey, long checkpoint, out NaturalKeyRecord naturalKeyRecord)
        {
            var naturalKeyRecords = this.store.GetOrAdd(aggregateRootType, _ => new ConcurrentDictionary<long, NaturalKeyRecord>());

            naturalKeyRecord = new NaturalKeyRecord
            {
                Identity = Guid.NewGuid(),
                SerializedValue = (string)serializedNaturalKey,
                Checkpoint = checkpoint + 1,
            };

            return naturalKeyRecords.TryAdd(this.lastCheckpoint = naturalKeyRecord.Checkpoint, naturalKeyRecord);
        }

        /// <summary>
        /// Removes the natural key with the specified identity from the natural key records.
        /// </summary>
        /// <param name="naturalKeyIdentity">The natural key identity.</param>
        public void Remove(Guid naturalKeyIdentity)
        {
            foreach (var naturalKeyRecords in this.store.Values)
            {
                var naturalKeyRecord = naturalKeyRecords.Values.SingleOrDefault(record => record.Identity == naturalKeyIdentity && !record.IsRemoved);
                if (naturalKeyRecord != null)
                {
                    NaturalKeyRecord record;
                    if (naturalKeyRecords.TryRemove(naturalKeyRecord.Checkpoint, out record))
                    {
                        record.Checkpoint = ++this.lastCheckpoint;
                        record.IsRemoved = true;
                        naturalKeyRecords.TryAdd(naturalKeyRecord.Checkpoint, naturalKeyRecord);
                    }
                }
            }
        }
    }
}
