// <copyright file="INaturalKeyRepository.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Sdk
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Exposes the public members of the natural key repository.
    /// </summary>
    public interface INaturalKeyRepository
    {
        /// <summary>
        /// Gets the natural key records for the specified type of aggregate root from the specified checkpoint.
        /// </summary>
        /// <param name="aggregateRootType">The aggregate root type.</param>
        /// <param name="checkpoint">The checkpoint.</param>
        /// <returns>The natural key records.</returns>
        IEnumerable<NaturalKeyRecord> GetNaturalKeys(Type aggregateRootType, long checkpoint);

        /// <summary>
        /// Attempts to add the natural key to the natural key records.
        /// </summary>
        /// <param name="aggregateRootType">The aggregate root type.</param>
        /// <param name="serializedNaturalKey">The serialized natural key.</param>
        /// <param name="checkpoint">The checkpoint.</param>
        /// <param name="naturalKeyRecord">The natural key record.</param>
        /// <returns>Returns <c>true</c> if the natural key record was successfully added; otherwise <c>false</c>.</returns>
        bool TryAddNaturalKey(Type aggregateRootType, object serializedNaturalKey, long checkpoint, out NaturalKeyRecord naturalKeyRecord);

        /// <summary>
        /// Removes the natural key with the specified identity from the natural key records.
        /// </summary>
        /// <param name="naturalKeyIdentity">The natural key identity.</param>
        void Remove(Guid naturalKeyIdentity);
    }
}
