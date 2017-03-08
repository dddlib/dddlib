// <copyright file="AggregateRootExtensions.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.TestFramework
{
    using System.Collections.Generic;

    /// <summary>
    /// Provides extension methods for the <see cref="AggregateRoot"/> clause.
    /// </summary>
    public static class AggregateRootExtensions
    {
        /// <summary>
        /// Gets the uncommitted events.
        /// </summary>
        /// <param name="aggregateRoot">The aggregate root.</param>
        /// <returns>The uncomitted events.</returns>
        public static IEnumerable<object> GetUncommittedEvents(this AggregateRoot aggregateRoot)
        {
            return aggregateRoot.GetUncommittedEvents();
        }

        /// <summary>
        /// Gets the memento.
        /// </summary>
        /// <param name="aggregateRoot">The aggregate root.</param>
        /// <returns>The memento.</returns>
        public static object GetMemento(this AggregateRoot aggregateRoot)
        {
            return aggregateRoot.GetMemento();
        }

        /// <summary>
        /// Gets the revision.
        /// </summary>
        /// <param name="aggregateRoot">The aggregate root.</param>
        /// <returns>The revision.</returns>
        public static int GetRevision(this AggregateRoot aggregateRoot)
        {
            return aggregateRoot.Revision;
        }
    }
}
