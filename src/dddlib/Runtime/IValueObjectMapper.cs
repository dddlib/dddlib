// <copyright file="IValueObjectMapper.cs" company="dddlib contributors">
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
    public interface IValueObjectMapper<TValueObject> : IFluentExtensions
        where TValueObject : ValueObject<TValueObject>
    {
#pragma warning restore 1591
        /// <summary>
        /// Specifies that the value object should be mapped to an event.
        /// </summary>
        /// <typeparam name="T">The type of event.</typeparam>
        /// <returns>The event.</returns>
        T ToEvent<T>() where T : new();

        /// <summary>
        /// Specifies that the value object should be mapped to an event.
        /// </summary>
        /// <typeparam name="T">The type of event.</typeparam>
        /// <param name="event">The event to map the value object to.</param>
        void ToEvent<T>(T @event);
    }
}