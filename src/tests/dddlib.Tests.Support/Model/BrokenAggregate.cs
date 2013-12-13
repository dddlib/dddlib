namespace dddlib.Tests.Support.Model
{
    using dddlib.Tests.Support.Events;

    public class BrokenAggregate : ChangeableAggregate
    {
        private void Apply(SomethingHappened @event)
        {
        }
    }
}
