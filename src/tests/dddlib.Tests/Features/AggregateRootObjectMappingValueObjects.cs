// <copyright file="AggregateRootObjectMappingValueObjects.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Features
{
    using dddlib.Configuration;
    using dddlib.Tests.Sdk;
    using FluentAssertions;
    using Xbehave;

    // As someone who uses dddlib
    // In order to create events from domain objects passed to [command] methods [on an aggregate root]
    // I need to be able to map between value objects and DTO's (to and from)
    public abstract class AggregateRootObjectMappingValueObjects : Feature
    {
        /*
            TODO (Cameron):
            So...
            You have an aggregate with a natural key value object
            You recreate the value object in the event handler
            The logic for the value object changes over time
            The re-creation fails upon reconstitution because the logic has changed
            The solution is... some sort of mapping...?

            1. ensure invalid (eg. throws exception) configuration is handled correctly.
            2. ensure missing mappings are handled correctly.
         */

        public class ValueObjectMappingWithEventCreation : AggregateRootObjectMappingValueObjects
        {
            [Scenario]
            public void Scenario(Subject instance, NaturalKey naturalKey)
            {
                "Given a natural key that is a value object"
                    .Given(() => naturalKey = new NaturalKey("naturalKey"));

                "When an instance of an aggregate root is created with that natural key"
                    .When(() => instance = new Subject(naturalKey));

                "Then the natural key of that instance should be the original natural key"
                    .Then(() => instance.NaturalKey.Should().Be(naturalKey));

                "And the instance should contain a single uncommitted 'NewSubject' event with a natural key value matching the original natural key value"
                    .And(() => instance.GetUncommittedEvents().Should().ContainSingle(
                        @event => @event is NewSubject && ((NewSubject)@event).NaturalKeyValue == naturalKey.Value));
            }

            public class Subject : AggregateRoot
            {
                public Subject(NaturalKey key)
                {
                    var @event = Map.ValueObject(key).ToEvent<NewSubject>();

                    this.Apply(@event);
                }

                internal Subject()
                {
                }

                public NaturalKey NaturalKey { get; private set; }

                private void Handle(NewSubject @event)
                {
                    this.NaturalKey = Map.Event(@event).ToValueObject<NaturalKey>();
                }
            }

            public class NaturalKey : ValueObject<NaturalKey>
            {
                public NaturalKey(string value)
                {
                    this.Value = value;
                }

                public string Value { get; private set; }
            }

            public class NewSubject
            {
                public string NaturalKeyValue { get; set; }
            }

            private class BootStrapper : IBootstrap<Subject>, IBootstrap<NaturalKey>
            {
                public void Bootstrap(IConfiguration configure)
                {
                    // TODO (Cameron): This is required in order to check the persisted events. Maybe give this some thought...?
                    configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());

                    configure.ValueObject<NaturalKey>()
                        .ToMapToEvent<NewSubject>((key, @event) => @event.NaturalKeyValue = key.Value, @event => new NaturalKey(@event.NaturalKeyValue));
                }
            }
        }

        public class ValueObjectMappingWithEventMutation : AggregateRootObjectMappingValueObjects
        {
            [Scenario]
            public void Scenario(Subject instance, Data data)
            {
                "Given an instance of an aggregate root with an identifier"
                    .Given(() => instance = new Subject { Id = "subjectId" });

                "And some data that is a value object"
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

                    Map.ValueObject(data).ToEvent(@event);

                    this.Apply(@event);
                }

                private void Handle(DataProcessed @event)
                {
                    this.ProcessedData = Map.Event(@event).ToValueObject<Data>();
                }
            }

            public class Data : ValueObject<Data>
            {
                public Data(string value)
                {
                    this.Value = value;
                }

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

                    configure.ValueObject<Data>()
                        .ToMapToEvent<DataProcessed>((data, @event) => @event.DataValue = data.Value, @event => new Data(@event.DataValue));
                }
            }
        }
    }
}
