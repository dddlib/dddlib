using dddlib.Configuration;
using dddlib.Tests.Support;

namespace dddlib.Tests.Features;

// As someone who uses dddlib
// In order to keep entities out of my events
// I need to be able to map between entities and events
public abstract class AggregateRootEntityMapping : Feature
{
    public sealed class EntityMappingWithEventCreation : AggregateRootEntityMapping
    {
        [Test]
        public async Task Scenario()
        {
            // Given a some thing that is an entity
            var thing = new Thing("naturalKey");

            // When an instance of an aggregate root is created with that thing
            var instance = new Subject(thing);

            // Then the thing of that instance should be the original thing
            await Assert.That(instance.Thing).IsEqualTo(thing);

            // And the instance should contain a single uncommitted 'NewSubject' event with a thing value matching the original thing value
            var events = instance.GetUncommittedEvents();
            await Assert.That(events).HasSingleItem();
            await Assert.That(events[0]).IsTypeOf<NewSubject>();
            await Assert.That(((NewSubject)events[0]).ThingValue).IsEqualTo(thing.Value);
        }

        public class Subject : AggregateRoot
        {
            public Subject(Thing thing)
            {
                var @event = this.Map.Entity(thing).ToEvent<NewSubject>();
                this.Apply(@event);
            }

            internal Subject()
            {
            }

            public Thing? Thing { get; private set; }

            private void Handle(NewSubject @event)
            {
                this.Thing = this.Map.Event(@event).ToEntity<Thing>();
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
            public string? ThingValue { get; set; }
        }

        private sealed class BootStrapper : IBootstrap<Subject>, IBootstrap<Thing>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
                configure.Entity<Thing>()
                    .ToMapToEvent<NewSubject>((thing, @event) => @event.ThingValue = thing.Value, @event => new Thing(@event.ThingValue!));
            }
        }
    }

    public sealed class EntityMappingWithEventMutation : AggregateRootEntityMapping
    {
        [Test]
        public async Task Scenario()
        {
            // Given an instance of an aggregate root with an identifier
            var instance = new Subject { Id = "subjectId" };

            // And some data that is an entity
            var data = new Data("dataValue");

            // When the instance processes that data
            instance.Process(data);

            // Then the processed data for the instance should be the original data
            await Assert.That(instance.ProcessedData).IsEqualTo(data);

            // And the instance should contain a single uncommitted 'DataProcessed' event with a data value matching the original data value
            var events = instance.GetUncommittedEvents();
            await Assert.That(events).HasSingleItem();
            await Assert.That(events[0]).IsTypeOf<DataProcessed>();
            await Assert.That(((DataProcessed)events[0]).DataValue).IsEqualTo(data.Value);
            await Assert.That(((DataProcessed)events[0]).SubjectId).IsEqualTo(instance.Id);
        }

        public class Subject : AggregateRoot
        {
            public string? Id { get; set; }

            public Data? ProcessedData { get; private set; }

            public void Process(Data data)
            {
                var @event = this.Map.Entity(data).ToEvent(new DataProcessed { SubjectId = this.Id });
                this.Apply(@event);
            }

            private void Handle(DataProcessed @event)
            {
                this.ProcessedData = this.Map.Event(@event).ToEntity<Data>();
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
            public string? SubjectId { get; set; }

            public string? DataValue { get; set; }
        }

        private sealed class BootStrapper : IBootstrap<Subject>, IBootstrap<Data>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
                configure.Entity<Data>()
                    .ToMapToEvent<DataProcessed>((data, @event) => @event.DataValue = data.Value, @event => new Data(@event.DataValue!));
            }
        }
    }
}
