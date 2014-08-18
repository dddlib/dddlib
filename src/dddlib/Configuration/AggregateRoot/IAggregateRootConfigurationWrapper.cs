// <copyright file="IAggregateRootConfigurationWrapper.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Configuration
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics.CodeAnalysis;

    [EditorBrowsable(EditorBrowsableState.Never)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1600:ElementsMustBeDocumented", Justification = "Not visible in editor.")]
    public interface IAggregateRootConfigurationWrapper<T> : IAggregateRootConfigurationWrapper<IAggregateRootConfigurationWrapper<T>, T>, IFluentExtensions
        where T : AggregateRoot
    {
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1600:ElementsMustBeDocumented", Justification = "Not visible in editor.")]
    public interface IAggregateRootConfigurationWrapper<TConfiguration, T> : IEntityConfigurationWrapper<TConfiguration, T>, IFluentExtensions
        where T : AggregateRoot
        where TConfiguration : IAggregateRootConfigurationWrapper<TConfiguration, T>
    {
        /// <summary>
        /// Configures the runtime to reconstitute the uninitialized aggregate root using the specified aggregate root factory.
        /// </summary>
        /// <param name="uninitializedFactory">The aggregate root factory.</param>
        /// <returns>The configuration.</returns>
        TConfiguration ToReconstituteUsing(Func<T> uninitializedFactory);
    }
}
