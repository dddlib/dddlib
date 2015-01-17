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

    /// <summary>
    /// Represents an aggregate root.
    /// </summary>
    public abstract class AggregateRoot : Entity
    {
        private readonly List<object> events = new List<object>();

        private readonly Lazy<IMapProvider> mapProvider = new Lazy<IMapProvider>(() => new MapperProvider(Application.Current.GetMapper()), true);
        
        private readonly AggregateRootType runtimeType;

        private string state;
        private bool isDestroyed;

        /// <summary>
        /// Initializes a new instance of the <see cref="AggregateRoot"/> class.
        /// </summary>
        protected AggregateRoot()
        {
            this.runtimeType = Application.Current.GetAggregateRootType(this.GetType());
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
        protected IMapProvider Map
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
                        "Unable to apply the specified change of type '{0}' to the aggregate root of type '{1}' as this instance of the aggregate root no longer exists",
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

            if (this.runtimeType.Options.DispatchEvents)
            {
                // TODO (Cameron): Add try... catch block.
                this.runtimeType.EventDispatcher.Dispatch(this, @event);
            }

            if (this.runtimeType.Options.PersistEvents && isNew)
            {
                this.events.Add(@event);
            }
        }
    }
}
