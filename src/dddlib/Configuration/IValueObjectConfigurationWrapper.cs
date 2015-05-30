// <copyright file="IValueObjectConfigurationWrapper.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics.CodeAnalysis;
    using dddlib.Runtime;
    using dddlib.Sdk;

#pragma warning disable 1591
    [EditorBrowsable(EditorBrowsableState.Never)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1600:ElementsMustBeDocumented", Justification = "Not visible in editor.")]
    public interface IValueObjectConfigurationWrapper<T> : IValueObjectConfigurationWrapper<IValueObjectConfigurationWrapper<T>, T>, IFluentExtensions
        where T : ValueObject<T>
    {
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1600:ElementsMustBeDocumented", Justification = "Not visible in editor.")]
    public interface IValueObjectConfigurationWrapper<TConfiguration, T> : IFluentExtensions
        where T : ValueObject<T>
        where TConfiguration : IValueObjectConfigurationWrapper<TConfiguration, T>
    {
#pragma warning restore 1591
        /// <summary>
        /// Configures the runtime to perform value object equality using the specified equality comparer.
        /// </summary>
        /// <param name="equalityComparer">The equality comparer.</param>
        /// <returns>The configuration.</returns>
        TConfiguration ToUseEqualityComparer(IEqualityComparer<T> equalityComparer);

        /// <summary>
        /// Configures the runtime to perform value object serialization using the specified serializer.
        /// </summary>
        /// <param name="valueObjectSerializer">The value object serializer.</param>
        /// <returns>The configuration.</returns>
        //// NOTE (Cameron): Value object serialization is a necessity because we don't control usage of a value object as a natural key.
        TConfiguration ToSerializeUsing(IValueObjectSerializer valueObjectSerializer);

        /// <summary>
        /// Configures the runtime to perform value object serialization using the specified serializer delegates.
        /// </summary>
        /// <param name="serialize">The serialization delegate.</param>
        /// <param name="deserialize">The deserialization delegate.</param>
        /// <returns>The configuration.</returns>
        TConfiguration ToSerializeUsing(Func<T, string> serialize, Func<string, T> deserialize);

        /// <summary>
        /// Configures the runtime to map the value object to the specified event type.
        /// </summary>
        /// <typeparam name="TEvent">The event type to map to.</typeparam>
        /// <param name="mapping">The mapping.</param>
        /// <returns>The configuration.</returns>
        TConfiguration ToMapToEvent<TEvent>(Action<T, TEvent> mapping);

        /// <summary>
        /// Configures the runtime to map the value object to the specified event type.
        /// </summary>
        /// <typeparam name="TEvent">The event type to map to.</typeparam>
        /// <param name="mapping">The mapping.</param>
        /// <param name="reverseMapping">The reverse mapping.</param>
        /// <returns>The configuration.</returns>
        TConfiguration ToMapToEvent<TEvent>(Action<T, TEvent> mapping, Func<TEvent, T> reverseMapping);
    }
}
