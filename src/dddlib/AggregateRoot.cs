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
        private readonly IEventDispatcher eventDispatcher;
        private readonly bool isTransient;

        private string state;
        private bool isDestroyed;

        /// <summary>
        /// Initializes a new instance of the <see cref="AggregateRoot"/> class.
        /// </summary>
        protected AggregateRoot()
        {
            var typeDescriptor = Application.Current.GetTypeDescriptor(this.GetType());
            
            this.eventDispatcher = typeDescriptor.EventDispatcher;
            this.isTransient = typeDescriptor.Factory == null;
        }

        internal string State
        {
            get { return this.state; }
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
            throw new RuntimeException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "The aggregate root of type '{0}' has not been configured to create a memento representing its state.",
                    this.GetType().Name));
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
        /// <param name="event">The event to apply.</param>
        protected void Apply(object @event)
        {
            Guard.Against.Null(() => @event);

            if (this.isDestroyed)
            {
                // TODO (Cameron): Use the natural key and type.
                // maybe: Unable to change Bob because a Person with that name no longer exists.
                // or: cannot change the aggregate of type Person with the identity Bob as this aggregates lifecycle has ended.
                throw new BusinessException("Unable to apply the specified change as this instance of the aggregate root no longer exists.");
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

            this.eventDispatcher.Dispatch(this, @event);

            if (isNew && !this.isTransient)
            {
                this.events.Add(@event);
            }
        }
    }
}
