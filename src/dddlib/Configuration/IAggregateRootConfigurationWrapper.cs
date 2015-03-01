// <copyright file="IAggregateRootConfigurationWrapper.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq.Expressions;
    using dddlib.Sdk;

#pragma warning disable 1591
    [EditorBrowsable(EditorBrowsableState.Never)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1600:ElementsMustBeDocumented", Justification = "Not visible in editor.")]
    public interface IAggregateRootConfigurationWrapper<T> : IAggregateRootConfigurationWrapper<IAggregateRootConfigurationWrapper<T>, T>, IFluentExtensions
        where T : AggregateRoot
    {
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1600:ElementsMustBeDocumented", Justification = "Not visible in editor.")]
    public interface IAggregateRootConfigurationWrapper<TConfiguration, T> : IFluentExtensions
        where T : AggregateRoot
        where TConfiguration : IAggregateRootConfigurationWrapper<TConfiguration, T>
    {
#pragma warning restore 1591
        /// <summary>
        /// Configures the runtime to reconstitute the uninitialized aggregate root using the specified aggregate root factory.
        /// </summary>
        /// <param name="uninitializedFactory">The aggregate root factory.</param>
        /// <returns>The configuration.</returns>
        TConfiguration ToReconstituteUsing(Func<T> uninitializedFactory);

        /// <summary>
        /// Configures the runtime to assign the natural key of the aggregate root using the specified natural key selector.
        /// </summary>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="naturalKeySelector">The natural key selector.</param>
        /// <returns>The configuration.</returns>
        TConfiguration ToUseNaturalKey<TKey>(Expression<Func<T, TKey>> naturalKeySelector);

        /// <summary>
        /// Configures the runtime to assign the natural key of the aggregate root using the specified natural key selector and string equality comparer.
        /// </summary>
        /// <param name="naturalKeySelector">The natural key selector.</param>
        /// <param name="equalityComparer">The string equality comparer for the natural key.</param>
        /// <returns>The configuration.</returns>
        TConfiguration ToUseNaturalKey(Expression<Func<T, string>> naturalKeySelector, IEqualityComparer<string> equalityComparer);
    }
}
