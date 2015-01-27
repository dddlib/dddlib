// <copyright file="IMapProvider.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk
{
    /// <summary>
    /// Exposes the public members of the map provider.
    /// </summary>
    public interface IMapProvider : IFluentExtensions
    {
        /// <summary>
        /// Specifies that an event should be mapped.
        /// </summary>
        /// <typeparam name="T">The type of event.</typeparam>
        /// <param name="event">The event.</param>
        /// <returns>A mapping specification.</returns>
        IEventMapper<T> Event<T>(T @event);

        /// <summary>
        /// Specifies that an entity should be mapped.
        /// </summary>
        /// <typeparam name="T">The type of entity.</typeparam>
        /// <param name="entity">The entity.</param>
        /// <returns>A mapping specification.</returns>
        IEntityMapper<T> Entity<T>(T entity) where T : Entity;

        /// <summary>
        /// Specifies that a value object should be mapped.
        /// </summary>
        /// <typeparam name="T">The type of value object.</typeparam>
        /// <param name="valueObject">The value object.</param>
        /// <returns>A mapping specification.</returns>
        IValueObjectMapper<T> ValueObject<T>(T valueObject) where T : ValueObject<T>;
    }
}