// <copyright file="IEntityMapper.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk
{
    /// <summary>
    /// Exposes the public members of the entity mapper.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    public interface IEntityMapper<TEntity> : IFluentExtensions
        where TEntity : Entity
    {
        /// <summary>
        /// Specifies that the entity should be mapped to an event.
        /// </summary>
        /// <typeparam name="T">The type of event.</typeparam>
        /// <returns>The event.</returns>
        T ToEvent<T>() where T : new();

        /// <summary>
        /// Specifies that the entity should be mapped to an event.
        /// </summary>
        /// <typeparam name="T">The type of event.</typeparam>
        /// <param name="event">The event to map the entity to.</param>
        void ToEvent<T>(T @event);
    }
}
