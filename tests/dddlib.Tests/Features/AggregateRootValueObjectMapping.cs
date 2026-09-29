using dddlib.Configuration;
using dddlib.Runtime;
using dddlib.Tests.Support;

namespace dddlib.Tests.Features;

// As someone who uses dddlib
// In order to keep value objects out of my events
// I need to be able to map between value objects and events
public abstract class AggregateRootValueObjectMapping : Feature
{
    public sealed class ValueObjectMappingWithEventCreation : AggregateRootValueObjectMapping
    {
        [Test]
        public async Task Scenario()
        {
            // Given a natural key that is a value object
            var naturalKey = new NaturalKey("naturalKey");

            // When an instance of an aggregate root is created with that natural key
            var instance = new Subject(naturalKey);

            // Then the natural key of that instance should be the original natural key
            await Assert.That(instance.NaturalKey).IsEqualTo(naturalKey);

            // And the instance should contain a single uncommitted 'NewSubject' event with a natural key value matching the original natural key value
            var events = instance.GetUncommittedEvents();
            await Assert.That(events).HasSingleItem();
            await Assert.That(events[0]).IsTypeOf<NewSubject>();
            await Assert.That(((NewSubject)events[0]).NaturalKeyValue).IsEqualTo(naturalKey.Value);
        }

        public class Subject : AggregateRoot
        {
            public Subject(NaturalKey key)
            {
                var @event = this.Map.ValueObject(key).ToEvent<NewSubject>();
                this.Apply(@event);
            }

            internal Subject()
            {
            }

            public NaturalKey? NaturalKey { get; private set; }

            private void Handle(NewSubject @event)
            {
                this.NaturalKey = this.Map.Event(@event).ToValueObject<NaturalKey>();
            }
        }

        public sealed record NaturalKey(string Value);

        public class NewSubject
        {
            public string? NaturalKeyValue { get; set; }
        }

        private sealed class BootStrapper : IBootstrap<Subject>, IBootstrap<NaturalKey>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
                configure.ValueObject<NaturalKey>()
                    .ToMapToEvent<NewSubject>((key, @event) => @event.NaturalKeyValue = key.Value, @event => new NaturalKey(@event.NaturalKeyValue!));
            }
        }
    }

    public sealed class ValueObjectMappingWithEventMutation : AggregateRootValueObjectMapping
    {
        [Test]
        public async Task Scenario()
        {
            // Given an instance of an aggregate root with an identifier
            var instance = new Subject { Id = "subjectId" };

            // And some data that is a value object
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
                var @event = this.Map.ValueObject(data).ToEvent(new DataProcessed { SubjectId = this.Id });
                this.Apply(@event);
            }

            private void Handle(DataProcessed @event)
            {
                this.ProcessedData = this.Map.Event(@event).ToValueObject<Data>();
            }
        }

        public sealed record Data(string Value);

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
                configure.ValueObject<Data>()
                    .ToMapToEvent<DataProcessed>((data, @event) => @event.DataValue = data.Value, @event => new Data(@event.DataValue!));
            }
        }
    }

    public sealed class ValueObjectMappingUndefined : AggregateRootValueObjectMapping
    {
        [Test]
        public async Task Scenario()
        {
            // Given a subject identifier
            var subjectId = new SubjectId("subjectId");

            // When a subject is created with that identifier
            var action = () => { _ = new Subject(subjectId); };

            // Then that action should throw a runtime exception (no mapping is configured for the value object)
            await Assert.That(action).Throws<RuntimeException>();
        }

        public class Subject : AggregateRoot
        {
            public Subject(SubjectId id)
            {
                this.Apply(this.Map.ValueObject(id).ToEvent<NewSubject>());
            }

            public string? Id { get; set; }
        }

        public sealed record SubjectId(string Value);

        public class NewSubject
        {
            public string? SubjectId { get; set; }
        }
    }

    public sealed class ValueObjectMappingPartiallyUndefined : AggregateRootValueObjectMapping
    {
        [Test]
        public async Task Scenario()
        {
            // Given a subject identifier
            var subjectId = new SubjectId("subjectId");

            // When a subject is created with that identifier
            var action = () => { _ = new Subject(subjectId); };

            // Then that action should throw a runtime exception (no reverse mapping is configured for the value object)
            await Assert.That(action).Throws<RuntimeException>();
        }

        public class Subject : AggregateRoot
        {
            public Subject(SubjectId id)
            {
                this.Apply(this.Map.ValueObject(id).ToEvent<NewSubject>());
            }

            public SubjectId? Id { get; set; }

            private void Handle(NewSubject @event)
            {
                this.Id = this.Map.Event(@event).ToValueObject<SubjectId>();
            }
        }

        public sealed record SubjectId(string Value);

        public class NewSubject
        {
            public string? SubjectId { get; set; }
        }

        private sealed class BootStrapper : IBootstrap<SubjectId>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.ValueObject<SubjectId>()
                    .ToMapToEvent<NewSubject>((subjectId, @event) => @event.SubjectId = subjectId.Value);
            }
        }
    }

    public sealed class EntityMappingUndefined : AggregateRootValueObjectMapping
    {
        [Test]
        public async Task Scenario()
        {
            // Given a subject identifier
            var subjectId = new SomeThing { Value = "subjectId" };

            // When a subject is created with that identifier
            var action = () => { _ = new Subject(subjectId); };

            // Then that action should throw a runtime exception (no mapping is configured for the entity)
            await Assert.That(action).Throws<RuntimeException>();
        }

        public class Subject : AggregateRoot
        {
            public Subject(SomeThing someThing)
            {
                this.Apply(this.Map.Entity(someThing).ToEvent<NewSubject>());
            }

            public string? SomeThing { get; set; }
        }

        public class SomeThing : Entity
        {
            public string? Value { get; set; }
        }

        public class NewSubject
        {
            public string? SomeThing { get; set; }
        }
    }

    public sealed class EntityMappingPartiallyUndefined : AggregateRootValueObjectMapping
    {
        [Test]
        public async Task Scenario()
        {
            // Given a subject identifier
            var subjectId = new SomeThing { Value = "subjectId" };

            // When a subject is created with that identifier
            var action = () => { _ = new Subject(subjectId); };

            // Then that action should throw a runtime exception (no reverse mapping is configured for the entity)
            await Assert.That(action).Throws<RuntimeException>();
        }

        public class Subject : AggregateRoot
        {
            public Subject(SomeThing someThing)
            {
                this.Apply(this.Map.Entity(someThing).ToEvent<NewSubject>());
            }

            public SomeThing? SomeThing { get; set; }

            private void Handle(NewSubject @event)
            {
                this.SomeThing = this.Map.Event(@event).ToEntity<SomeThing>();
            }
        }

        public class SomeThing : Entity
        {
            public string? Value { get; set; }
        }

        public class NewSubject
        {
            public string? SomeThing { get; set; }
        }

        private sealed class BootStrapper : IBootstrap<SomeThing>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.Entity<SomeThing>()
                    .ToMapToEvent<NewSubject>((subjectId, @event) => @event.SomeThing = subjectId.Value);
            }
        }
    }
}
