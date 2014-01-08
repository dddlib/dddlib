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

        public object[] ThingsThatHappened
        {
            get { return this.thingsThatHappened.ToArray(); }
        }

        public void MakeSomethingHappen()
        {
            this.ApplyChange(new SomethingHappened());
        }

        private void Apply(SomethingHappened @event)
        {
            this.thingsThatHappened.Add(@event);
        }
    }
}
