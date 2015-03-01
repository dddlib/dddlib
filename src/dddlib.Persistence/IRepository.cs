// <copyright file="IRepository.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence
{
    /// <summary>
    /// Exposes the public members of a repository.
    /// </summary>
    /// <typeparam name="T">The type of aggregate root.</typeparam>
    public interface IRepository<T> where T : AggregateRoot
    {
        /// <summary>
        /// Saves the specified aggregate root.
        /// </summary>
        /// <param name="aggregateRoot">The aggregate root.</param>
        void Save(T aggregateRoot);

        /// <summary>
        /// Loads the aggregate root with the specified natural key.
        /// </summary>
        /// <param name="naturalKey">The natural key.</param>
        /// <returns>The aggregate root.</returns>
        T Load(object naturalKey);
    }
}
