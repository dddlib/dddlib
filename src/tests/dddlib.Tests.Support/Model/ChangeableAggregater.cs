namespace dddlib.Tests.Support.Model
{
    using dddlib.Tests.Support.Events;

    public class ChangeableAggregate : AggregateRoot
    {
        [NaturalKey]
        public string NaturalKey
        {
            get { return string.Empty; }
        }

        public object Change { get; private set; }

        public void ApplyEvent(object change)
        {
            this.Apply(change);
        }

        private void Handle(SomethingHappened @event)
        {
            this.Change = @event;
        }

        private void Handle(SomethingElseHappened @event, int count)
        {
            this.Change = @event;
        }
    }
}
