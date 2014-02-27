// <copyright file="IConfigureAggregateRoots.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime.Configuration
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics.CodeAnalysis;

    /*  NOTE (Cameron): 
        This is where all the configuration options for aggregate roots go.
        The complexity of the interfaces is not lost on me.  */

    /// <summary>
    /// Exposes the public members of the aggregate roots configuration.
    /// </summary>
    /// <typeparam name="TConfiguration">The type of the configuration.</typeparam>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IConfigureAggregateRoots<TConfiguration> : IFluentExtensions
        where TConfiguration : IConfigureAggregateRoots<TConfiguration>
    {
        /// <summary>
        /// Configures the runtime to not dispatch any events.
        /// </summary>
        //// NOTE (Cameron): This returns void instead of TConfiguration because no other options should be available following this option.
        void ToNotDispatchEvents();

        /// <summary>
        /// Configures the runtime to use the specified event dispatcher factory to determine the event dispatcher to use for dispatching events.
        /// </summary>
        /// <param name="eventDispatcherFactory">The event dispatcher factory.</param>
        /// <returns>The configuration.</returns>
        ////TConfiguration ToDispatchEventsUsing(Func<Type, IEventDispatcher> eventDispatcherFactory);
    }

    /// <summary>
    /// Exposes the public members of the aggregate root configuration.
    /// </summary>
    /// <typeparam name="TConfiguration">The type of the configuration.</typeparam>
    /// <typeparam name="T">The type of aggregate root.</typeparam>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IConfigureAggregateRoot<TConfiguration, T> : IConfigureEntity<TConfiguration, T>, IConfigureAggregateRoots<TConfiguration>, IFluentExtensions
        where T : AggregateRoot
        where TConfiguration : IConfigureAggregateRoot<TConfiguration, T>
    {
        /// <summary>
        /// Configures the runtime to reconstitute the uninitialized aggregate root using the specified aggregate root factory.
        /// </summary>
        /// <param name="uninitializedFactory">The aggregate root factory.</param>
        /// <returns>The configuration.</returns>
        TConfiguration ToReconstituteUsing(Func<T> uninitializedFactory);
    }

    [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1124:DoNotUseRegions", Justification = "Here the code is meant to be hidden.")]
    #region ** Don't bother touching anything in this region **

    /// <summary>
    /// Exposes the public members of the aggregate roots configuration.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IConfigureAggregateRoots : IConfigureAggregateRoots<IConfigureAggregateRoots>, IFluentExtensions
    {
    }

    /// <summary>
    /// Exposes the public members of the aggregate root configuration.
    /// </summary>
    /// <typeparam name="T">The type of aggregate root.</typeparam>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IConfigureAggregateRoot<T> : IConfigureAggregateRoot<IConfigureAggregateRoot<T>, T>, IFluentExtensions
        where T : AggregateRoot
    {
    }

    #endregion
}
