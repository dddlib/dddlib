using dddlib.Configuration;
using dddlib.TestFramework;
using dddlib.Tests.Support;

namespace dddlib.Tests.Features.Generated;

// As someone who uses dddlib with event sourcing
// In order to persist events
// I need to be able to record changes in state
public abstract partial class AggregateRootEventApplication : Feature
{
    public sealed partial class EventsAreStoredOnAggregate : AggregateRootEventApplication
    {
        [Test]
        public async Task Scenario()
        {
            // Given a natural key
            var naturalKey = "key";

            // When an aggregate root is instantiated with that natural key
            var aggregateRoot = new Subject(naturalKey);

            // Then an event is raised with that natural key
            var events = aggregateRoot.GetUncommittedEvents();
            await Assert.That(events).HasSingleItem();
            await Assert.That(events[0]).IsTypeOf<NewSubject>();
            await Assert.That(((NewSubject)events[0]).NaturalKey).IsEqualTo(naturalKey);
        }

        public partial class Subject : AggregateRoot
        {
            public Subject(string key)
            {
                this.Apply(new NewSubject { NaturalKey = key });
            }

            internal Subject()
            {
            }

            [NaturalKey]
            public string? NaturalKey { get; private set; }

            private void Handle(NewSubject @event)
            {
                this.NaturalKey = @event.NaturalKey;
            }
        }

        private sealed partial class NewSubject
        {
            public string? NaturalKey { get; set; }
        }

        private sealed partial class Bootstrapper : IBootstrap<Subject>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
            }
        }
    }

    public sealed partial class EventsAreStoredOnInheritedAggregate : AggregateRootEventApplication
    {
        [Test]
        public async Task Scenario()
        {
            // Given a registration service
            var registrationService = new RegistrationService();

            // And a registration
            var registration = new Registration("abc", registrationService);

            // When a van is instantiated with that registration
            var van = new Van(registration);

            // Then an event is raised for the new vehicle
            var events = van.GetUncommittedEvents();
            await Assert.That(events).Count().IsEqualTo(2);
            await Assert.That(events).Contains(
                @event => @event is NewVehicle newVehicle && newVehicle.RegistrationNumber == registration.Number);

            // And an event is raised for the new van
            await Assert.That(events).Contains(
                @event => @event is NewVan newVan && newVan.RegistrationNumber == registration.Number);
        }

        public partial class Van : Vehicle
        {
            public Van(Registration registration)
                : base(registration)
            {
                this.Apply(new NewVan { RegistrationNumber = registration.Number });
            }

            protected internal Van()
            {
            }
        }

        public partial class NewVan
        {
            public string? RegistrationNumber { get; set; }
        }

        public partial class RegistrationService : IRegistrationService
        {
            public bool ConfirmValid(string registrationNumber) => true;
        }

        private sealed partial class Bootstrapper : IBootstrap<Van>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.AggregateRoot<Van>().ToReconstituteUsing(() => new Van());
            }
        }
    }

    public sealed partial class InheritedEventsAreStoredOnInheritedAggregate : AggregateRootEventApplication
    {
        [Test]
        public async Task Scenario()
        {
            // Given a registration service
            var registrationService = new RegistrationService();

            // And a registration
            var registration = new Registration("abc", registrationService);

            // When a van is instantiated with that registration
            var van = new Van(registration);

            // Then an event is raised for the new vehicle
            var events = van.GetUncommittedEvents();
            await Assert.That(events).Count().IsEqualTo(2);
            await Assert.That(events).Contains(
                @event => @event.GetType() == typeof(NewVehicle) && ((NewVehicle)@event).RegistrationNumber == registration.Number);

            // And an event is raised for the new van (which inherits from the new vehicle event)
            await Assert.That(events).Contains(
                @event => @event is NewVan newVan && newVan.RegistrationNumber == registration.Number);
        }

        public partial class Van : Vehicle
        {
            public Van(Registration registration)
                : base(registration)
            {
                this.Apply(new NewVan { RegistrationNumber = registration.Number });
            }

            protected internal Van()
            {
            }
        }

        public partial class NewVan : NewVehicle
        {
        }

        public partial class RegistrationService : IRegistrationService
        {
            public bool ConfirmValid(string registrationNumber) => true;
        }

        private sealed partial class Bootstrapper : IBootstrap<Van>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.AggregateRoot<Van>().ToReconstituteUsing(() => new Van());
            }
        }
    }

    // A positional record has no parameterless constructor (https://github.com/dddlib/dddlib/issues/48).
    public sealed partial class PositionalRecordEventsAreStoredOnAggregate : AggregateRootEventApplication
    {
        [Test]
        public async Task Scenario()
        {
            // Given a natural key
            var naturalKey = "key";

            // When an aggregate root that applies a positional record is instantiated with that natural key
            var aggregateRoot = new Subject(naturalKey);

            // Then the event is dispatched to its handler
            await Assert.That(aggregateRoot.NaturalKey).IsEqualTo(naturalKey);

            // And the event is raised with that natural key
            var events = aggregateRoot.GetUncommittedEvents();
            await Assert.That(events).HasSingleItem();
            await Assert.That(events[0]).IsEqualTo(new NewSubject(naturalKey));
        }

        public partial class Subject : AggregateRoot
        {
            public Subject(string key)
            {
                this.Apply(new NewSubject(key));
            }

            internal Subject()
            {
            }

            [NaturalKey]
            public string? NaturalKey { get; private set; }

            private void Handle(NewSubject @event)
            {
                this.NaturalKey = @event.NaturalKey;
            }
        }

        private sealed partial record NewSubject(string NaturalKey);

        private sealed partial class Bootstrapper : IBootstrap<Subject>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
            }
        }
    }
}
