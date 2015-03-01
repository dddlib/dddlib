namespace dddlib.Tests.Support.Model
{
    public class BadAggregate : ChangeableAggregate
    {
        public object BadChange { get; private set; }

        private void Handle(int @event)
        {
            this.BadChange = @event;
        }

        private void Handle(int? @event)
        {
            this.BadChange = @event;
        }
    }
}
