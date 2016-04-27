// <copyright file="Bug0109.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Tests.Bug
{
    using System;
    using dddlib.Tests.Sdk;
    using FluentAssertions;
    using Memory;
    using SqlServer;
    using Xunit;

    public class Bug0109 : dddlib.Tests.Sdk.Integration.Database
    {
        public Bug0109(SqlServerFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public void ShouldThrowForMemoryEventStoreRepository()
        {
            // arrange
            var repository = new MemoryEventStoreRepository();
            var naturalKey = "key";

            // act
            var subject = new EventBasedSubject(naturalKey);
            repository.Save(subject);
            var sameSubject = repository.Load<EventBasedSubject>(subject.NaturalKey);
            subject.Destroy();
            repository.Save(subject);
            sameSubject.Change();
            Action action = () => repository.Save(sameSubject);

            // assert
            action.ShouldThrow<ConcurrencyException>();
        }

        [Fact]
        public void ShouldThrowForMemoryRepository()
        {
            // arrange
            var repository = new MemoryRepository<ConventionalSubject>();
            var naturalKey = "key";

            // act
            var subject = new ConventionalSubject(naturalKey);
            repository.Save(subject);
            var sameSubject = repository.Load(subject.NaturalKey);
            subject.Destroy();
            repository.Save(subject);
            Action action = () => repository.Save(sameSubject);

            // assert
            action.ShouldThrow<ConcurrencyException>();
        }

        [Fact]
        public void ShouldThrowForSqlServerEventStoreRepository()
        {
            // arrange
            var repository = new SqlServerEventStoreRepository(this.ConnectionString);
            var naturalKey = "key";

            // act
            var subject = new EventBasedSubject(naturalKey);
            repository.Save(subject);
            var sameSubject = repository.Load<EventBasedSubject>(subject.NaturalKey);
            subject.Destroy();
            repository.Save(subject);
            sameSubject.Change();
            Action action = () => repository.Save(sameSubject);

            // assert
            action.ShouldThrow<ConcurrencyException>();
        }

        private class ConventionalSubject : AggregateRoot
        {
            public ConventionalSubject(string naturalKey)
            {
                this.NaturalKey = naturalKey;
            }

            protected internal ConventionalSubject()
            {
            }

            [NaturalKey]
            public string NaturalKey { get; private set; }

            public void Destroy()
            {
                this.EndLifecycle();
            }

            protected override object GetState()
            {
                return new Memento { NaturalKey = this.NaturalKey };
            }

            protected override void SetState(object memento)
            {
                var subject = (Memento)memento;
                this.NaturalKey = subject.NaturalKey;
            }

            private class Memento
            {
                public string NaturalKey { get; set; }
            }
        }

        private class EventBasedSubject : AggregateRoot
        {
            public EventBasedSubject(string naturalKey)
            {
                this.Apply(new SubjectCreated { NaturalKey = naturalKey });
            }

            protected internal EventBasedSubject()
            {
            }

            [NaturalKey]
            public string NaturalKey { get; private set; }

            public void Change()
            {
                this.Apply(new SubjectChanged { NaturalKey = this.NaturalKey });
            }

            public void Destroy()
            {
                this.Apply(new SubjectDestroyed { NaturalKey = this.NaturalKey });
            }

            private void Handle(SubjectCreated @event)
            {
                this.NaturalKey = @event.NaturalKey;
            }

            private void Handle(SubjectDestroyed @event)
            {
                this.EndLifecycle();
            }
        }

        private class SubjectCreated
        {
            public string NaturalKey { get; set; }
        }

        private class SubjectChanged
        {
            public string NaturalKey { get; set; }
        }

        private class SubjectDestroyed
        {
            public string NaturalKey { get; set; }
        }
    }
}
