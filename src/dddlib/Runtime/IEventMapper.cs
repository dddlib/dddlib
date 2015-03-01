// <copyright file="IEventMapper.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System.ComponentModel;
    using System.Diagnostics.CodeAnalysis;
    using dddlib.Sdk;

#pragma warning disable 1591
    [EditorBrowsable(EditorBrowsableState.Never)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1600:ElementsMustBeDocumented", Justification = "Not visible in editor.")]
    public interface IEventMapper<TEvent> : IFluentExtensions
    {
#pragma warning restore 1591
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
