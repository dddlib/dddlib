// <copyright file="IConfigureValueObject.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Exposes the public members of the value object configuration.
    /// </summary>
    /// <typeparam name="TConfiguration">The type of the configuration.</typeparam>
    /// <typeparam name="T">The type of value object.</typeparam>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IConfigureValueObject<TConfiguration, T> : IFluentExtensions
        where T : ValueObject<T>
        where TConfiguration : IConfigureValueObject<TConfiguration, T>
    {
        /// <summary>
        /// Configures the runtime to map the value object to the specified type.
        /// </summary>
        /// <typeparam name="TOut">The type to map to.</typeparam>
        /// <param name="mapping">The mapping.</param>
        /// <returns>The configuration.</returns>
        TConfiguration ToMapAs<TOut>(Func<T, TOut> mapping);

        /// <summary>
        /// Configures the runtime to perform value object equality using the specified equality comparer.
        /// </summary>
        /// <param name="equalityComparer">The equality comparer.</param>
        /// <returns>The configuration.</returns>
        TConfiguration ToUseEqualityComparer(IEqualityComparer<T> equalityComparer);
    }

    [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1124:DoNotUseRegions", Justification = "Here the code is meant to be hidden.")]
    #region ** Don't bother touching anything in this region **

    /// <summary>
    /// Exposes the public members of the value object configuration.
    /// </summary>
    /// <typeparam name="T">The type of entity.</typeparam>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IConfigureValueObject<T> : IConfigureValueObject<IConfigureValueObject<T>, T>, IFluentExtensions
        where T : ValueObject<T>
    {
    }

    #endregion
}
