// <copyright file="AggregateRootFactory.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

[module: System.Diagnostics.CodeAnalysis.SuppressMessage(
    "StyleCop.CSharp.NamingRules",
    "SA1300:ElementMustBeginWithUpperCaseLetter",
    Justification = "Noted.")]

namespace dddlib.Persistence
{
    /*  TODO (Cameron): 
        Consider a different (non-ES) repository - maybe just many overloads?
        Cache factory per aggregate.  */

    using System;
    using System.Collections.Generic;
    using dddlib.Runtime;

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
            var aggregate = Application.Current.GetAggregateRootDescriptor(typeof(T)).Factory() as IAggregateRoot;
            aggregate.Initialize(memento, events, state);
            return (T)aggregate;
        }

        // IMPORTANT (Cameron): If we save a car, which is a vehicle to the vehicle repo and the natural key is inherited, what happens?
        // I think each repo needs to ensure it's saving the correct type.
    }
}
