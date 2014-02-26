// <copyright file="IConfiguration.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Exposes the public members of the configuration.
    /// </summary>
    //// NOTE (Cameron): Set to never be visible in the editor. Not sue if this is a sensible design choice... but it is a design choice.
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IConfiguration
    {
        /// <summary>
        /// Sets the event dispatcher factory to use for all aggregate roots in the assembly containing the implementation.
        /// </summary>
        /// <param name="factory">The event dispatcher factory.</param>
        void SetEventDispatcherFactory(Func<Type, IEventDispatcher> factory);

        /// <summary>
        /// Registers the specified factory for creating an uninitialized instance of an aggregate of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of aggregate root.</typeparam>
        /// <param name="factory">The factory for the aggregate root.</param>
        void RegisterAggregateRootFactory<T>(Func<T> factory) where T : AggregateRoot;

        [SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "Not my call.")]
        [SuppressMessage("Microsoft.Naming", "CA1716:IdentifiersShouldNotMatchKeywords", MessageId = "GetType", Justification = "This is it.")]
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1600:ElementsMustBeDocumented", Justification = "Not visible in editor.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        Type GetType();

        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1600:ElementsMustBeDocumented", Justification = "Not visible in editor.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        int GetHashCode();

        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1600:ElementsMustBeDocumented", Justification = "Not visible in editor.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        string ToString();

        [SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames", MessageId = "obj", Justification = "Not my call")]
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1600:ElementsMustBeDocumented", Justification = "Not visible in editor.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        bool Equals(object obj);
    }
}
