using dddlib.Runtime;
using dddlib.Sdk;

namespace dddlib.Tests.Unit;

public class AggregateRootTests
{
    [Test]
    public async Task CanInstantiateEmptyAggregate()
    {
        var action = () => { _ = new EmptyAggregate(); };

        await Assert.That(action).ThrowsNothing();
    }

    [Test]
    public async Task CanApplyHandledChangeToAggregate()
    {
        var aggregate = new ChangeableAggregate();
        var @event = new SomethingHappened();

        aggregate.ApplyEvent(@event);

        await Assert.That(aggregate.Change).IsSameReferenceAs(@event);
    }

    [Test]
    public async Task CanApplyMishandledChangeToAggregateWithoutEffectOrException()
    {
        var aggregate = new ChangeableAggregate();
        var @event = new SomethingElseHappened();

        aggregate.ApplyEvent(@event);

        await Assert.That(aggregate.Change).IsNull();
    }

    [Test]
    public async Task CanApplyHandledInheritedChangeToAggregateWithoutEffectOrException()
    {
        var aggregate = new ChangeableAggregate();
        var @event = new SomethingWierdHappened();

        aggregate.ApplyEvent(@event);

        await Assert.That(aggregate.Change).IsNull();
    }

    [Test]
    public async Task CanApplyUnhandledChangeToAggregateWithoutEffectOrException()
    {
        var aggregate = new ChangeableAggregate();
        var @event = new SomethingNeverHappened();

        aggregate.ApplyEvent(@event);

        await Assert.That(aggregate.Change).IsNull();
    }

    [Test]
    public async Task CannotApplyInvalidChangeToAggregate()
    {
        var aggregate = new BadAggregate();
        var @event = 1;

        var action = () => aggregate.ApplyEvent(@event);

        await Assert.That(action).Throws<RuntimeException>();
    }

    [Test]
    public async Task CannotApplyOtherInvalidChangeToAggregate()
    {
        var aggregate = new BadAggregate();
        int? @event = 1;

        var action = () => aggregate.ApplyEvent(@event);

        await Assert.That(action).Throws<RuntimeException>();
    }

    [Test]
    public async Task CanApplyMultipleHandledChangeToAggregate()
    {
        var aggregate = new MoreChangeableAggregate();
        var @event = new SomethingHappened();

        aggregate.ApplyEvent(@event);

        await Assert.That(aggregate.Change).IsSameReferenceAs(@event);
        await Assert.That(aggregate.OtherChange).IsSameReferenceAs(@event);
    }

    [Test]
    public async Task CannotHandleChangeFromBaseAggregate()
    {
        var aggregate = new OverridingAggregate();
        var @event = new SomethingWierdHappened();

        aggregate.ApplyEvent(@event);

        await Assert.That(aggregate.Change).IsNull();
    }

    [Test]
    public async Task CanEndLifecycle()
    {
        var aggregate = new LifecycleAggregate();
        aggregate.DoSomething(); // proves we can do something before we destroy
        aggregate.Destroy();

        var action = aggregate.DoSomething;

        await Assert.That(action).Throws<BusinessException>();
    }

    [Test]
    public async Task CanReconstitute()
    {
        var memento = default(object);
        var events = new[] { new SomethingHappened() };
        var factory = new AggregateRootFactory();

        var aggregateRoot = factory.Create<PersistedAggregate>(memento, 0, events, "state");
        aggregateRoot.MakeSomethingHappen();

        await Assert.That(aggregateRoot.ThingsThatHappened).Count().IsEqualTo(2);
    }

    public class SomethingHappened
    {
    }

    public class SomethingElseHappened
    {
    }

    public class SomethingWierdHappened : SomethingHappened
    {
    }

    public class SomethingNeverHappened
    {
    }

    public class LifecycleEnded
    {
    }

    public class ChangeableAggregate : AggregateRoot
    {
        [NaturalKey]
        public string NaturalKey => string.Empty;

        public object? Change { get; private set; }

        public void ApplyEvent(object change) => this.Apply(change);

        private void Handle(SomethingHappened @event) => this.Change = @event;

#pragma warning disable IDE0051, IDE0060 // a two-parameter handler must be ignored by the dispatcher
        private void Handle(SomethingElseHappened @event, int count) => this.Change = @event;
#pragma warning restore IDE0051, IDE0060, CA1822
    }

    public class BadAggregate : ChangeableAggregate
    {
        public object? BadChange { get; private set; }

#pragma warning disable IDE0051 // value-type handlers must be ignored by the dispatcher
        private void Handle(int @event) => this.BadChange = @event;

        private void Handle(int? @event) => this.BadChange = @event;
#pragma warning restore IDE0051
    }

    public class EmptyAggregate : AggregateRoot
    {
        [NaturalKey]
        public string NaturalKey => string.Empty;
    }

    public class LifecycleAggregate : AggregateRoot
    {
        [NaturalKey]
        public string NaturalKey => string.Empty;

        public void Destroy() => this.Apply(new LifecycleEnded());

        public void DoSomething() => this.Apply(new SomethingHappened());

        private void Handle(LifecycleEnded @event) => this.EndLifecycle();
    }

    public class MoreChangeableAggregate : ChangeableAggregate
    {
        public object? OtherChange { get; private set; }

        private void Handle(SomethingHappened @event) => this.OtherChange = @event;
    }

    public class OverridingAggregate : ChangeableAggregate
    {
#pragma warning disable IDE0051, IDE0060, CA1822 // a method not named Handle must be ignored by the dispatcher
        private void Apply(SomethingWierdHappened @event)
        {
        }
#pragma warning restore IDE0051, IDE0060, CA1822
    }

    public class PersistedAggregate : AggregateRoot
    {
        private readonly List<object> thingsThatHappened = [];

        [NaturalKey]
        public string NaturalKey => string.Empty;

        public IReadOnlyList<object> ThingsThatHappened => this.thingsThatHappened;

        public void MakeSomethingHappen() => this.Apply(new SomethingHappened());

        private void Handle(SomethingHappened @event) => this.thingsThatHappened.Add(@event);
    }
}
