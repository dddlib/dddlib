namespace dddlib.Tests.Support.Model
{
    using dddlib.Tests.Support.Events;

    public class MoreChangeableAggregate : ChangeableAggregate
    {
        public object OtherChange { get; private set; }

        private void Handle(SomethingHappened @event)
        {
            this.OtherChange = @event;
        }
    }
}
