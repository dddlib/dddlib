namespace dddlib.Tests.Support.Model
{
    using dddlib.Tests.Support.Events;

    public class ChangeableAggregate : AggregateRoot
    {
        public object Change { get; protected set; }

        public void ApplyEvent(object change)
        {
            this.ApplyChange(change);
        }

        private void Apply(SomethingHappened @event)
        {
            this.Change = @event;
        }

        private void Apply(SomethingElseHappened @event, int count)
        {
            this.Change = @event;
        }
    }
}
