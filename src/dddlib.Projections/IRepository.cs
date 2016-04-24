// <copyright file="IRepository.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Projections
{
    using System.Collections.Generic;

    /// <summary>
    /// Exposes the public members of a repository.
    /// </summary>
    /// <typeparam name="TIdentity">The type of the identity.</typeparam>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    public interface IRepository<TIdentity, TEntity>
        where TEntity : class
    {
        /// <summary>
        /// Gets the entity with the specified identity.
        /// </summary>
        /// <param name="identity">The identity.</param>
        /// <returns>The entity.</returns>
        TEntity Get(TIdentity identity);

        /// <summary>
        /// Adds or updates the entity with the specified identity.
        /// </summary>
        /// <param name="identity">The identity.</param>
        /// <param name="entity">The entity to add or update.</param>
        void AddOrUpdate(TIdentity identity, TEntity entity);

        /// <summary>
        /// Removes the entity with the specified identity.
        /// </summary>
        /// <param name="identity">The identity.</param>
        void Remove(TIdentity identity);

        /// <summary>
        /// Purges the contents of repository.
        /// </summary>
        void Purge();

        /// <summary>
        /// Performs a bulk update against the contents of the repository.
        /// </summary>
        /// <param name="addOrUpdate">The entities to add or update.</param>
        /// <param name="remove">The identities of the entities to remove.</param>
        void BulkUpdate(IEnumerable<KeyValuePair<TIdentity, TEntity>> addOrUpdate, IEnumerable<TIdentity> remove);
    }
}
