// <copyright file="IConfigureEntity.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Exposes the public members of the entity configuration.
    /// </summary>
    /// <typeparam name="TConfiguration">The type of the configuration.</typeparam>
    /// <typeparam name="T">The type of entity.</typeparam>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IConfigureEntity<TConfiguration, T> : IFluentExtensions
        where T : Entity
        where TConfiguration : IConfigureEntity<TConfiguration, T>
    {
        /// <summary>
        /// Configures the runtime to assign the natural key of entity using the specified natural key selector.
        /// </summary>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="naturalKeySelector">The natural key selector.</param>
        /// <returns>The configuration.</returns>
        TConfiguration ToUseNaturalKey<TKey>(Func<T, TKey> naturalKeySelector);

        /// <summary>
        /// Configures the runtime to assign the natural key of entity using the specified natural key selector and string equality comparer.
        /// </summary>
        /// <param name="naturalKeySelector">The natural key selector.</param>
        /// <param name="equalityComparer">The string equality comparer for the natural key.</param>
        /// <returns>The configuration.</returns>
        TConfiguration ToUseNaturalKey(Func<T, string> naturalKeySelector, IEqualityComparer<string> equalityComparer);
    }

    [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1124:DoNotUseRegions", Justification = "Here the code is meant to be hidden.")]
    #region ** Don't bother touching anything in this region **

    /// <summary>
    /// Exposes the public members of the entity configuration.
    /// </summary>
    /// <typeparam name="T">The type of entity.</typeparam>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IConfigureEntity<T> : IConfigureEntity<IConfigureEntity<T>, T>, IFluentExtensions
        where T : Entity
    {
    }

    #endregion
}
