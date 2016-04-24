// <copyright file="MemoryRepository.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Projections.Memory
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;

    /// <summary>
    /// Represents a memory repository.
    /// </summary>
    /// <typeparam name="TIdentity">The type of the identity.</typeparam>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <seealso cref="dddlib.Projections.IRepository{TIdentity, TEntity}" />
    public class MemoryRepository<TIdentity, TEntity> : IRepository<TIdentity, TEntity>
        where TEntity : class
    {
        private readonly ConcurrentDictionary<TIdentity, TEntity> entities;

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryRepository{TIdentity, TEntity}"/> class.
        /// </summary>
        public MemoryRepository()
            : this(EqualityComparer<TIdentity>.Default)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryRepository{TIdentity, TEntity}"/> class.
        /// </summary>
        /// <param name="equalityComparer">The equality comparer.</param>
        public MemoryRepository(IEqualityComparer<TIdentity> equalityComparer)
        {
            Guard.Against.Null(() => equalityComparer);

            this.entities = new ConcurrentDictionary<TIdentity, TEntity>(equalityComparer);
        }

        /// <summary>
        /// Gets the entity with the specified identity.
        /// </summary>
        /// <param name="identity">The identity.</param>
        /// <returns>The entity.</returns>
        public virtual TEntity Get(TIdentity identity)
        {
            TEntity entity;
            return this.entities.TryGetValue(identity, out entity) ? entity : default(TEntity);
        }

        /// <summary>
        /// Gets all the entities.
        /// </summary>
        /// <returns>All the entities.</returns>
        public virtual IEnumerable<KeyValuePair<TIdentity, TEntity>> GetAll()
        {
            return this.entities;
        }

        /// <summary>
        /// Adds or updates the entity with the specified identity.
        /// </summary>
        /// <param name="identity">The identity.</param>
        /// <param name="entity">The entity to add or update.</param>
        public virtual void AddOrUpdate(TIdentity identity, TEntity entity)
        {
            this.entities.AddOrUpdate(identity, entity, (i, e) => entity);
        }

        /// <summary>
        /// Removes the entity with the specified identity.
        /// </summary>
        /// <param name="identity">The identity.</param>
        public virtual void Remove(TIdentity identity)
        {
            TEntity entity;
            this.entities.TryRemove(identity, out entity);
        }

        /// <summary>
        /// Purges the contents of repository.
        /// </summary>
        public virtual void Purge()
        {
            this.entities.Clear();
        }

        /// <summary>
        /// Performs a bulk update against the contents of the repository.
        /// </summary>
        /// <param name="addOrUpdate">The entities to add or update.</param>
        /// <param name="remove">The identities of the entities to remove.</param>
        public virtual void BulkUpdate(IEnumerable<KeyValuePair<TIdentity, TEntity>> addOrUpdate, IEnumerable<TIdentity> remove)
        {
            foreach (var item in addOrUpdate)
            {
                this.entities.AddOrUpdate(item.Key, item.Value, (i, e) => item.Value);
            }

            TEntity entity = null;
            foreach (var identity in remove)
            {
                this.entities.TryRemove(identity, out entity);
            }
        }
    }
}
