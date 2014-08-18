// <copyright file="IEntityConfigurationWrapper.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq.Expressions;

    [EditorBrowsable(EditorBrowsableState.Never)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1600:ElementsMustBeDocumented", Justification = "Not visible in editor.")]
    public interface IEntityConfigurationWrapper<T> : IEntityConfigurationWrapper<IEntityConfigurationWrapper<T>, T>, IFluentExtensions
        where T : Entity
    {
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1600:ElementsMustBeDocumented", Justification = "Not visible in editor.")]
    public interface IEntityConfigurationWrapper<TConfiguration, T> : IFluentExtensions
        where T : Entity
        where TConfiguration : IEntityConfigurationWrapper<TConfiguration, T>
    {
        /// <summary>
        /// Configures the runtime to assign the natural key of entity using the specified natural key selector.
        /// </summary>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="naturalKeySelector">The natural key selector.</param>
        /// <returns>The configuration.</returns>
        TConfiguration ToUseNaturalKey<TKey>(Expression<Func<T, TKey>> naturalKeySelector);

        /// <summary>
        /// Configures the runtime to assign the natural key of entity using the specified natural key selector and string equality comparer.
        /// </summary>
        /// <param name="naturalKeySelector">The natural key selector.</param>
        /// <param name="equalityComparer">The string equality comparer for the natural key.</param>
        /// <returns>The configuration.</returns>
        TConfiguration ToUseNaturalKey(Expression<Func<T, string>> naturalKeySelector, IEqualityComparer<string> equalityComparer);
    }
}
