// <copyright file="ITypeAnalyzerService.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk.Configuration.Model
{
    using System;

    /// <summary>
    /// Exposes the public members of the type analyzer service.
    /// </summary>
    public interface ITypeAnalyzerService
    {
        /// <summary>
        /// Determines whether the specified runtime type is a valid aggregate root.
        /// </summary>
        /// <param name="runtimeType">The runtime type.</param>
        /// <returns>Returns <c>true</c> when the runtime type is an aggregate root; otherwise <c>false</c>.</returns>
        bool IsValidAggregateRoot(Type runtimeType);

        /// <summary>
        /// Determines whether the specified runtime type is a valid entity.
        /// </summary>
        /// <param name="runtimeType">The runtime type.</param>
        /// <returns>Returns <c>true</c> when the runtime type is an entity; otherwise <c>false</c>.</returns>
        bool IsValidEntity(Type runtimeType);

        /// <summary>
        /// Determines whether the specified runtime type is a valid value object.
        /// </summary>
        /// <param name="runtimeType">The runtime type.</param>
        /// <returns>Returns <c>true</c> when the runtime type is a value object; otherwise <c>false</c>.</returns>
        bool IsValidValueObject(Type runtimeType);

        /// <summary>
        /// Determines whether the specified runtime type contains a specific property.
        /// </summary>
        /// <param name="runtimeType">The runtime type.</param>
        /// <param name="propertyName">The property name.</param>
        /// <param name="propertyType">The property type.</param>
        /// <returns>Returns <c>true</c> when the runtime type contains the property; otherwise <c>false</c>.</returns>
        bool IsValidProperty(Type runtimeType, string propertyName, Type propertyType);

        /// <summary>
        /// Gets the natural key for the specified runtime type.
        /// </summary>
        /// <param name="runtimeType">The runtime type.</param>
        /// <returns>The natural key.</returns>
        NaturalKey GetNaturalKey(Type runtimeType);

        /// <summary>
        /// Gets the uninitialized factory for the specified runtime type.
        /// </summary>
        /// <param name="runtimeType">The runtime type.</param>
        /// <returns>The uninitialized factory.</returns>
        Delegate GetUninitializedFactory(Type runtimeType);
    }
}