// <copyright file="AggregateRootEventApplication.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Feature
{
    using dddlib.Configuration;
    using dddlib.Tests.Sdk;
    using FluentAssertions;
    using Xbehave;

    // As someone who uses dddlib [with event sourcing]
    // In order to persist events
    // I need to be able to record changes in state
    public abstract class AggregateRootEventApplication : Feature
    {
        /*
            Can change name of aggregate apply method
            Can inherit from assembly where aggregate apply method is different
        */

        public class UndefinedNaturalKeySelector : AggregateRootEventApplication
        {
            [Scenario]
            public void CanInstantiate(string naturalKey)
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
                    configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
                }
            }
        }
    }
}
