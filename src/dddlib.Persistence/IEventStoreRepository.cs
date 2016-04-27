// <copyright file="IEventStoreRepository.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence
{
    using System;

    /// <summary>
    /// Exposes the public members of the event store repository.
    /// </summary>
    public interface IEventStoreRepository
    {
        /// <summary>
        /// Saves the specified aggregate root.
        /// </summary>
        /// <typeparam name="T">The type of aggregate root.</typeparam>
        /// <param name="aggregateRoot">The aggregate root.</param>
        void Save<T>(T aggregateRoot) where T : AggregateRoot;

        /// <summary>
        /// Saves the specified aggregate root.
        /// </summary>
        /// <typeparam name="T">The type of aggregate root.</typeparam>
        /// <param name="aggregateRoot">The aggregate root.</param>
        /// <param name="correlationId">The correlation identifier.</param>
        void Save<T>(T aggregateRoot, Guid correlationId) where T : AggregateRoot;

        /// <summary>
        /// Loads the aggregate root with the specified natural key.
        /// </summary>
        /// <typeparam name="T">The type of aggregate root.</typeparam>
        /// <param name="naturalKey">The natural key.</param>
        /// <returns>The aggregate root.</returns>
        T Load<T>(object naturalKey) where T : AggregateRoot;
    }
}
