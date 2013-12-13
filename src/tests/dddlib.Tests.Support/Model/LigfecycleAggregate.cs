namespace dddlib.Tests.Support.Model
{
    using dddlib.Tests.Support.Events;

    public class LifecycleAggregate : AggregateRoot
    {
        public void EndLifecycle()
        {
            this.ApplyChange(new LifecycleEnded());
        }

        public void DoSomething()
        {
            this.ApplyChange(new SomethingHappened());
        }

        private void Apply(LifecycleEnded @event)
        {
            this.Destroy();
        }
    }
}
