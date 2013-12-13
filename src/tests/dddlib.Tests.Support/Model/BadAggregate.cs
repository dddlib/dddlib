namespace dddlib.Tests.Support.Model
{
    public class BadAggregate : ChangeableAggregate
    {
        private void Apply(int @event)
        {
            this.Change = @event;
        }

        private void Apply(int? @event)
        {
            this.Change = @event;
        }
    }
}
