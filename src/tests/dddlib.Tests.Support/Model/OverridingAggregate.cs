namespace dddlib.Tests.Support.Model
{
    using dddlib.Tests.Support.Events;

    // TODO (Cameron): Ensure that overriding routes ALL Apply calls through the overridden method.
    // TODO (Cameron): Ensure that base overrides of the method continue to get called without base.Apply().
    // TODO (Cameron): Consider usage - especially in sub-classing scenarios.
    // TODO (Cameron): An event should only be handled (privately) inside of the class where it is raised.
    public class OverridingAggregate : ChangeableAggregate
    {
        private void Apply(SomethingWierdHappened @event)
        {
            ////this.Change = @event;
        }

        ////public object Change { get; protected set; }

        ////public void ApplyEvent(object change)
        ////{
        ////    this.ApplyChange(change);
        ////}

        ////protected override void Apply(dynamic @event)
        ////{
        ////    switch ((string)@event.GetType().Name)
        ////    {
        ////        case "SomethingHappened":
        ////        case "SomethingElseHappened":
        ////            this.Change = @event;
        ////            break;
        ////    }

        ////    base.Apply((object)@event);
        ////}
    }
}
