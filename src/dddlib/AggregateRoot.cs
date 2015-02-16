// <copyright file="AggregateRoot.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib
{
    /*  TODO (Cameron): 
        Enable ES without persistence mode.
        Fix exception types (as before).
        Remove IAggregateRoot.
        More will be required once persistence layer is fleshed out.
        Consider enforcing logic around change of state field.  */

    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using dddlib.Runtime;
    using dddlib.Sdk;
    using dddlib.Sdk.Configuration.Model;

    /// <summary>
    /// Represents an aggregate root.
    /// </summary>
    public abstract class AggregateRoot : Entity
    {
        private readonly List<object> events = new List<object>();

        // TODO (Cameron): Add to configuration.
        private readonly Lazy<IMapperProvider> mapProvider = new Lazy<IMapperProvider>(() => new DefaultMapperProvider(), true);
        
        private readonly IEventDispatcher eventDispatcher;
        private readonly bool dispatchEvents;
        private readonly bool persistEvents;

        private string state;
        private bool isDestroyed;

        // TODO (Cameron): Fix.
        internal AggregateRoot(IEventDispatcher eventDispatcher, bool dispatchEvents, bool persistEvents)
        {
            Guard.Against.Null(() => eventDispatcher);

            this.eventDispatcher = eventDispatcher;
            this.dispatchEvents = dispatchEvents;
            this.persistEvents = persistEvents;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AggregateRoot"/> class.
        /// </summary>
        protected AggregateRoot()
            : this(@this => Config.From(Application.Current.GetAggregateRootType(@this.GetType())))
        {
        }

        // LINK (Cameron): http://stackoverflow.com/questions/2287636/pass-current-object-type-into-base-constructor-call
        private AggregateRoot(Func<AggregateRoot, Config> configureAggregateRoot)
        {
            var configuration = configureAggregateRoot(this);

            this.eventDispatcher = configuration.EventDispatcher;
            this.dispatchEvents = configuration.DispatchEvents;
            this.persistEvents = configuration.PersistEvents;
        }

        internal string State
        {
            get { return this.state; }
        }

        /// <summary>
        /// Specifies that a mapping should take place.
        /// </summary>
        /// <value>The mapping options.</value>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1623:PropertySummaryDocumentationMustMatchAccessors", Justification = "Not here.")]
        protected IMapperProvider Map
        {
            get { return this.mapProvider.Value; }
        }

        internal void Initialize(object memento, IEnumerable<object> events, string state)
        {
            Guard.Against.Null(() => events);

            if (memento != null)
            {
                this.SetState(memento);
            }

            foreach (var @event in events)
            {
                this.Apply(@event, isNew: false);
            }

            this.state = state;
        }

        internal object GetMemento()
        {
            return this.GetState();
        }

        internal IEnumerable<object> GetUncommittedEvents()
        {
            return this.events;
        }

        internal void CommitEvents(string state)
        {
            Guard.Against.NullOrEmpty(() => state);

            this.events.Clear();
            this.state = state;
        }

        /// <summary>
        /// Gets a memento representing the state of the aggregate root.
        /// </summary>
        /// <returns>A memento representing the state of the aggregate root.</returns>
        [SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "Inappropriate.")]
        protected virtual object GetState()
        {
            ////throw new RuntimeException(
            ////    string.Format(
            ////        CultureInfo.InvariantCulture,
            ////        "The aggregate root of type '{0}' has not been configured to create a memento representing its state.",
            ////        this.GetType().Name));

            return null;
        }

        /// <summary>
        /// Sets the state of the aggregate root from the specified memento.
        /// </summary>
        /// <param name="memento">A memento representing the state of the aggregate root.</param>
        protected virtual void SetState(object memento)
        {
            throw new RuntimeException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "The aggregate root of type '{0}' has not been configured to apply a memento representing its state.",
                    this.GetType().Name));
        }

        /// <summary>
        /// Ends the lifecycle of this instance of the aggregate root.
        /// </summary>
        protected void EndLifecycle()
        {
            this.isDestroyed = true;
        }

        /// <summary>
        /// Applies the specified event to the aggregate root.
        /// </summary>
        /// <typeparam name="T">The type of event.</typeparam>
        /// <param name="event">The event to apply.</param>
        //// NOTE (Cameron): The type constraints on the event stem from the requirements of the mapper functionality.
        protected void Apply<T>(T @event) where T : class, new()
        {
            Guard.Against.Null(() => @event);

            if (this.isDestroyed)
            {
                // TODO (Cameron): Use the natural key and type.
                // maybe: Unable to change Bob because a Person with that name no longer exists.
                // or: cannot change the aggregate of type Person with the identity Bob as this aggregates lifecycle has ended.
                throw new BusinessException(
                    string.Format(
                        "Unable to apply the specified change of type '{0}' to the aggregate root of type '{1}' as the lifecycle of this instance of the aggregate root has been terminated.",
                        @event.GetType(),
                        this.GetType()));
            }

            this.Apply(@event, isNew: true);
        }

        private void Apply(object @event, bool isNew)
        {
            Guard.Against.Null(() => @event);

            if (!@event.GetType().IsClass)
            {
                return;
            }

            if (this.dispatchEvents)
            {
                // TODO (Cameron): Add try... catch block.
                this.eventDispatcher.Dispatch(this, @event);
            }

            if (this.persistEvents && isNew)
            {
                this.events.Add(@event);
            }
        }

        private class Config
        {
            public IEventDispatcher EventDispatcher { get; set; }

            public bool DispatchEvents { get; set; }

            public bool PersistEvents { get; set; }

            public static Config From(AggregateRootType aggregateRootType)
            {
                return new Config { EventDispatcher = aggregateRootType.EventDispatcher, DispatchEvents = aggregateRootType.DispatchEvents, PersistEvents = aggregateRootType.PersistEvents };
            }
        }
    }
}
