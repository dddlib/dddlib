// <copyright file="DefaultMapperProvider.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk
{
    using dddlib.Runtime;

    /// <summary>
    /// Represents the default mapper provider.
    /// </summary>
    public sealed class DefaultMapperProvider : IMapperProvider
    {
        /// <summary>
        /// Specifies that an event should be mapped.
        /// </summary>
        /// <typeparam name="T">The type of event.</typeparam>
        /// <param name="event">The event.</param>
        /// <returns>A mapping specification.</returns>
        public IEventMapper<T> Event<T>(T @event)
        {
            return new DefaultEventMapper<T>(@event);
        }

        /// <summary>
        /// Specifies that an entity should be mapped.
        /// </summary>
        /// <typeparam name="T">The type of entity.</typeparam>
        /// <param name="entity">The entity.</param>
        /// <returns>A mapping specification.</returns>
        public IEntityMapper<T> Entity<T>(T entity) where T : Entity
        {
            return new DefaultEntityMapper<T>(entity);
        }

        /// <summary>
        /// Specifies that a value object should be mapped.
        /// </summary>
        /// <typeparam name="T">The type of value object.</typeparam>
        /// <param name="valueObject">The value object.</param>
        /// <returns>A mapping specification.</returns>
        public IValueObjectMapper<T> ValueObject<T>(T valueObject) where T : ValueObject<T>
        {
            return new DefaultValueObjectMapper<T>(valueObject);
        }
    }
}
