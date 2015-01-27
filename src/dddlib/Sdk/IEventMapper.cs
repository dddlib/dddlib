// <copyright file="IEventMapper.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk
{
    /// <summary>
    /// Exposes the public members of the event mapper.
    /// </summary>
    /// <typeparam name="TEvent">The type of the event.</typeparam>
    public interface IEventMapper<TEvent> : IFluentExtensions
    {
        /// <summary>
        /// Specifies that the event should be mapped to an entity.
        /// </summary>
        /// <typeparam name="T">The type of entity.</typeparam>
        /// <returns>The entity.</returns>
        T ToEntity<T>() where T : Entity;

        /// <summary>
        /// Specifies that the event should be mapped to a value object.
        /// </summary>
        /// <typeparam name="T">The type of value object.</typeparam>
        /// <returns>The value object.</returns>
        T ToValueObject<T>() where T : ValueObject<T>;
    }
}
