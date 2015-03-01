// <copyright file="IConfiguration.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Configuration
{
    using System.ComponentModel;
    using System.Diagnostics.CodeAnalysis;
    using dddlib.Sdk;

#pragma warning disable 1591
    [EditorBrowsable(EditorBrowsableState.Never)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1600:ElementsMustBeDocumented", Justification = "Not visible in editor.")]
    public interface IConfiguration : IFluentExtensions
    {
#pragma warning restore 1591
        /// <summary>
        /// Gets the aggregate root configuration options for the specified type of aggregate root.
        /// </summary>
        /// <typeparam name="T">The type of aggregate root.</typeparam>
        /// <returns>The aggregate root configuration options.</returns>
        IAggregateRootConfigurationWrapper<T> AggregateRoot<T>() where T : AggregateRoot;

        /// <summary>
        /// Gets the entity configuration options for the specified type of entity.
        /// </summary>
        /// <typeparam name="T">The type of entity.</typeparam>
        /// <returns>The entity configuration options.</returns>
        IEntityConfigurationWrapper<T> Entity<T>() where T : Entity;

        /// <summary>
        /// Gets the value object configuration options for the specified type of value object.
        /// </summary>
        /// <typeparam name="T">The type of value object.</typeparam>
        /// <returns>The value object configuration options.</returns>
        IValueObjectConfigurationWrapper<T> ValueObject<T>() where T : ValueObject<T>;
    }
}
