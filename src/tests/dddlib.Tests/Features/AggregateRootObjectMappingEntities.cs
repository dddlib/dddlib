// <copyright file="AggregateRootObjectMappingEntities.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Features
{
    using System;
    using dddlib.Configuration;
    using dddlib.Tests.Sdk;
    using FluentAssertions;
    using Xbehave;

    // As someone who uses dddlib
    // In order to create events from domain objects passed to [command] methods [on an aggregate root]
    // I need to be able to map between entities and DTO's (to and from)
    public abstract class AggregateRootObjectMappingEntities : Feature
    {
        public class EntityMappingWithEventCreation : AggregateRootObjectMappingEntities
        {
            [Scenario]
            public void Scenario(Subject instance, Thing thing)
            {
                "Given a some thing that is an entity"
                    .Given(() => thing = new Thing("naturalKey"));

                "When an instance of an aggregate root is created with that thing"
                    .When(() => instance = new Subject(thing));

                "Then the thing of that instance should be the original thing"
                    .Then(() => instance.Thing.Should().Be(thing));

                "And the instance should contain a single uncommitted 'NewSubject' event with a thing value matching the original thing value"
                    .And(() => instance.GetUncommittedEvents().Should().ContainSingle(
                        @event => @event is NewSubject && ((NewSubject)@event).ThingValue == thing.Value));
            }

            public class Subject : AggregateRoot
            {
                public Subject(Thing thing)
                {
                    var @event = Map.Entity(thing).ToEvent<NewSubject>();

                    this.Apply(@event);
                }

                internal Subject()
                {
                }

                public Thing Thing { get; private set; }

                private void Handle(NewSubject @event)
                {
                    this.Thing = Map.Event(@event).ToEntity<Thing>();
                }
            }

            public class Thing : Entity
            {
                public Thing(string value)
                {
                    this.Value = value;
                }

                [NaturalKey]
                public string Value { get; private set; }
            }

            public class NewSubject
            {
                public string ThingValue { get; set; }
            }

            private class BootStrapper : IBootstrap<Subject>, IBootstrap<Thing>
            {
                public void Bootstrap(IConfiguration configure)
                {
                    // TODO (Cameron): This is required in order to check the persisted events. Maybe give this some thought...?
                    configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());

                    configure.Entity<Thing>()
                        .ToMapToEvent<NewSubject>((thing, @event) => @event.ThingValue = thing.Value, @event => new Thing(@event.ThingValue));
                }
            }
        }

        public class EntityMappingWithEventMutation : AggregateRootObjectMappingEntities
        {
            [Scenario]
            public void Scenario(Subject instance, Data data)
            {
                "Given an instance of an aggregate root with an identifier"
                    .Given(() => instance = new Subject { Id = "subjectId" });

                "And some data that is an entity"
                    .And(() => data = new Data("dataValue"));

                "When the instance processes that data"
                    .When(() => instance.Process(data));

                "Then the processed data for the instance should be the original data"
                    .Then(() => instance.ProcessedData.Should().Be(data));

                "And the instance should contain a single uncommitted 'DataProcessed' event with a data value matching the original data value"
                    .And(() => instance.GetUncommittedEvents().Should().ContainSingle(
                        @event => @event is DataProcessed && ((DataProcessed)@event).DataValue == data.Value));
            }

            public class Subject : AggregateRoot
            {
                public string Id { get; set; }

                public Data ProcessedData { get; private set; }

                public void Process(Data data)
                {
                    var @event = new DataProcessed { SubjectId = this.Id };

                    Map.Entity(data).ToEvent(@event);

                    this.Apply(@event);
                }

                private void Handle(DataProcessed @event)
                {
                    this.ProcessedData = Map.Event(@event).ToEntity<Data>();
                }
            }

            public class Data : Entity
            {
                public Data(string value)
                {
                    this.Value = value;
                }

                [NaturalKey]
                public string Value { get; private set; }
            }

            public class DataProcessed
            {
                public string SubjectId { get; set; }

                public string DataValue { get; set; }
            }

            private class BootStrapper : IBootstrap<Subject>, IBootstrap<Data>
            {
                public void Bootstrap(IConfiguration configure)
                {
                    // TODO (Cameron): This is required in order to check the persisted events. Maybe give this some thought...?
                    configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());

                    configure.Entity<Data>()
                        .ToMapToEvent<DataProcessed>((data, @event) => @event.DataValue = data.Value, @event => new Data(@event.DataValue));
                }
            }
        }
    }
}
