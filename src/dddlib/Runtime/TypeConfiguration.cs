// <copyright file="TypeConfiguration.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;

    /// <summary>
    /// Represents the type configuration.
    /// </summary>
    public sealed class TypeConfiguration
    {
        private readonly IEventDispatcher eventDispatcher;
        private readonly Func<object> aggregateRootFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="TypeConfiguration"/> class.
        /// </summary>
        public TypeConfiguration()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TypeConfiguration"/> class.
        /// </summary>
        /// <param name="eventDispatcher">The event dispatcher.</param>
        public TypeConfiguration(IEventDispatcher eventDispatcher)
            : this()
        {
            Guard.Against.Null(() => eventDispatcher);

            this.eventDispatcher = eventDispatcher;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TypeConfiguration" /> class.
        /// </summary>
        /// <param name="eventDispatcher">The event dispatcher.</param>
        /// <param name="aggregateRootFactory">The aggregate root.</param>
        public TypeConfiguration(IEventDispatcher eventDispatcher, Func<object> aggregateRootFactory)
            : this(eventDispatcher)
        {
            Guard.Against.Null(() => aggregateRootFactory);

            this.aggregateRootFactory = aggregateRootFactory;
        }

        /// <summary>
        /// Gets the event dispatcher.
        /// </summary>
        /// <value>The event dispatcher.</value>
        public IEventDispatcher EventDispatcher
        {
            get { return this.eventDispatcher; }
        }

        /// <summary>
        /// Gets the aggregate root factory.
        /// </summary>
        /// <value>The aggregate root factory.</value>
        public Func<object> AggregateRootFactory
        {
            get { return this.aggregateRootFactory; }
        }
    }
}
