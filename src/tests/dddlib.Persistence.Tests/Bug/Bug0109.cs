// <copyright file="Bug0109.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Tests.Bug
{
    using System;
    using FluentAssertions;
    using Memory;
    using Persistence.Sdk;
    using Xunit;

    public class Bug0109
    {
        [Fact]
        public void ShouldThrowForMemoryEventStoreRepository()
        {
            // arrange
            var repository = new MemoryEventStoreRepository();
            var naturalKey = "key";

            // act
            var subject = new Subject(naturalKey);
            repository.Save(subject);
            var sameSubject = repository.Load<Subject>(subject.NaturalKey);
            subject.Destroy();
            repository.Save(subject);
            sameSubject.Change();
            Action action = () => repository.Save(sameSubject);

            // assert
            action.ShouldThrow<ConcurrencyException>();
        }

        private class Subject : AggregateRoot
        {
            public Subject(string naturalKey)
            {
                this.Apply(new SubjectCreated { NaturalKey = naturalKey });
            }

            protected internal Subject()
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
