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
    public class MemoryNaturalKeyRepository : INaturalKeyRepository
    {
        private readonly ConcurrentDictionary<Type, ConcurrentDictionary<long, NaturalKeyRecord>> store =
            new ConcurrentDictionary<Type, ConcurrentDictionary<long, NaturalKeyRecord>>();

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
                Checkpoint = checkpoint,
            };

            return naturalKeyRecords.TryAdd(checkpoint, naturalKeyRecord);
        }
    }
}
