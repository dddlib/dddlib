// <copyright file="AggregateRootEventApplication.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Feature
{
    using dddlib.Configuration;
    using dddlib.TestFramework;
    using dddlib.Tests.Sdk;
    using dddlib.Tests.Support;
    using FluentAssertions;
    using Xbehave;

    // As someone who uses dddlib [with event sourcing]
    // In order to persist events
    // I need to be able to record changes in state
    public abstract class AggregateRootEventApplication : Feature
    {
        /*
            TODO (Cameron):
            Can change name of aggregate apply method
            Can inherit from assembly where aggregate apply method is different
        */

        public class EventsAreStoredOnAggregate : AggregateRootEventApplication
        {
            [Scenario]
            public void Scenario(string naturalKey)
            {
                var aggregateRoot = default(Subject);

                "Given a natural key"
                    .f(() => naturalKey = "key");

                "When an aggregate root is instantiated with that natural key"
                    .f(() => aggregateRoot = new Subject(naturalKey));

                "Then an event is raised with that natural key"
                    .f(() => aggregateRoot.GetUncommittedEvents().Should().ContainSingle(@event => @event.As<NewSubject>().NaturalKey == naturalKey));
            }

            public class Subject : AggregateRoot
            {
                public Subject(string key)
                {
                    this.Apply(new NewSubject { NaturalKey = key });
                }

                internal Subject()
                {
                }

                [NaturalKey]
                public string NaturalKey { get; private set; }

                private void Handle(NewSubject @event)
                {
                    this.NaturalKey = @event.NaturalKey;
                }
            }

            private class NewSubject
            {
                public string NaturalKey { get; set; }
            }

            private class Bootstrapper : IBootstrap<Subject>
            {
                public void Bootstrap(IConfiguration configure)
                {
                    // TODO (Cameron): This is required in order to check the persisted events. Maybe give this some thought...?
                    configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
                }
            }
        }

        public class EventsAreStoredOnInheritedAggregate : AggregateRootEventApplication
        {
            [Scenario]
            public void Scenario(IRegistrationService registrationService, Registration registration, Van van)
            {
                "Given a registration service"
                    .f(() => registrationService = new RegistrationService());

                "And a registration"
                    .f(() => registration = new Registration("abc", registrationService));

                "When a van is instantiated with that registration"
                    .f(() => van = new Van(registration));

                "Then an event is raised for the new vehicle"
                    .f(() => van.GetUncommittedEvents().Should().Contain(@event => @event.As<NewVehicle>().RegistrationNumber == registration.Number));

                "And an event is raised for the new van"
                    .f(() => van.GetUncommittedEvents().Should().Contain(@event => (@event.As<NewVan>() ?? new NewVan()).RegistrationNumber == registration.Number));
            }

            public class Van : Vehicle
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

            public class NewVan
            {
                public string RegistrationNumber { get; set; }
            }

            public class RegistrationService : IRegistrationService
            {
                public bool ConfirmValid(string registrationNumber)
                {
                    return true;
                }
            }

            private class Bootstrapper : IBootstrap<Van>
            {
                public void Bootstrap(IConfiguration configure)
                {
                    // TODO (Cameron): This is required in order to check the persisted events. Maybe give this some thought...?
                    configure.AggregateRoot<Van>().ToReconstituteUsing(() => new Van());
                }
            }
        }

        public class InheritedEventsAreStoredOnInheritedAggregate : AggregateRootEventApplication
        {
            [Scenario]
            public void Scenario(IRegistrationService registrationService, Registration registration, Van van)
            {
                "Given a registration service"
                    .f(() => registrationService = new RegistrationService());

                "And a registration"
                    .f(() => registration = new Registration("abc", registrationService));

                "When a van is instantiated with that registration"
                    .f(() => van = new Van(registration));

                "Then an event is raised for the new vehicle"
                    .f(() => van.GetUncommittedEvents().Should().Contain(@event => @event.As<NewVehicle>().RegistrationNumber == registration.Number));

                "And an event is raised for the new van"
                    .f(() => van.GetUncommittedEvents().Should().Contain(@event => (@event.As<NewVan>() ?? new NewVan()).RegistrationNumber == registration.Number));
            }

            public class Van : Vehicle
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

            public class NewVan : NewVehicle
            {
            }

            public class RegistrationService : IRegistrationService
            {
                public bool ConfirmValid(string registrationNumber)
                {
                    return true;
                }
            }

            private class Bootstrapper : IBootstrap<Van>
            {
                public void Bootstrap(IConfiguration configure)
                {
                    // TODO (Cameron): This is required in order to check the persisted events. Maybe give this some thought...?
                    configure.AggregateRoot<Van>().ToReconstituteUsing(() => new Van());
                }
            }
        }
    }
}
