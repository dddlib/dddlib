// <copyright file="PersistedAggregate.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Support.Model
{
    using System.Collections.Generic;
    using dddlib.Tests.Support.Events;

    public class PersistedAggregate : AggregateRoot
    {
        private readonly List<object> thingsThatHappened = new List<object>();

        // TODO (Cameron): Consider a solution that uses internals to build - reverse using InternalsVisibleTo.
        protected PersistedAggregate()
        {
            this.thingsThatHappened = new List<object>();
        }

        [NaturalKey]
        public string NaturalKey
        {
            get { return string.Empty; }
        }

        public object[] ThingsThatHappened
        {
            get { return this.thingsThatHappened.ToArray(); }
        }

        public void MakeSomethingHappen()
        {
            this.Apply(new SomethingHappened());
        }

        private void Handle(SomethingHappened @event)
        {
            this.thingsThatHappened.Add(@event);
        }
    }
}
