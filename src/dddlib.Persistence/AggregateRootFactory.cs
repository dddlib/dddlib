// <copyright file="AggregateRootFactory.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

[module: System.Diagnostics.CodeAnalysis.SuppressMessage(
    "StyleCop.CSharp.NamingRules",
    "SA1300:ElementMustBeginWithUpperCaseLetter",
    Justification = "Noted.")]

namespace dddlib.Persistence
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A factory that provides the ability to reconstitute an aggregate root.
    /// </summary>
    public sealed class AggregateRootFactory
    {
        /// <summary>
        /// Reconstitutes the specified aggregate root.
        /// </summary>
        /// <typeparam name="T">The type of aggregate root.</typeparam>
        /// <param name="memento">The memento, or null.</param>
        /// <param name="events">The events that contain the state of the aggregate root.</param>
        /// <param name="state">A checksum that correlates to the state of the aggregate root in the persistence layer for the given events.</param>
        /// <returns>The specified aggregate root.</returns>
        public T Reconstitute<T>(object memento, IEnumerable<object> events, string state) 
            where T : AggregateRoot
        {
            // TODO (Cameron): Make this more performant. Consider using some type of IL instantiation.
            var aggregate = Activator.CreateInstance(typeof(T), true) as IAggregateRoot;
            aggregate.Initialize(memento, events, state);
            return (T)aggregate;
        }
    }
}
