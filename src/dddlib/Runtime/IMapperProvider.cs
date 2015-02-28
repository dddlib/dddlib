// <copyright file="IMapperProvider.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System.ComponentModel;
    using System.Diagnostics.CodeAnalysis;
    using dddlib.Sdk;

    ////[EditorBrowsable(EditorBrowsableState.Never)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1600:ElementsMustBeDocumented", Justification = "Not visible in editor.")]
    public interface IMapperProvider : IFluentExtensions
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