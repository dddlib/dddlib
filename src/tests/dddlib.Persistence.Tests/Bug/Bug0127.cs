// <copyright file="Bug0127.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Tests.Bug
{
    using System;
    using dddlib.Persistence.SqlServer;
    using dddlib.Tests.Sdk;
    using FluentAssertions;
    using Xunit;

    public class Bug0127 : dddlib.Tests.Sdk.Integration.Database
    {
        public Bug0127(SqlServerFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public void ShouldWorkForMultipleAggregateRootTypes()
        {
            // arrange
            var repository = new SqlServerEventStoreRepository(this.ConnectionString);
            var subject = new Subject("subject");
            var otherSubject = new OtherSubject("otherSubject");
            repository.Save(subject);
            repository.Save(otherSubject);

            // NOTE (Cameron): This requires a new identity map...
            repository = new SqlServerEventStoreRepository(this.ConnectionString);

            // act
            var sameSubject = repository.Load<Subject>(subject.NaturalKey);
            Action action = () => repository.Load<OtherSubject>(otherSubject.NaturalKey);

            // assert
            action.ShouldNotThrow();
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

            private void Handle(SubjectCreated @event)
            {
                this.NaturalKey = @event.NaturalKey;
            }
        }

        private class OtherSubject : Subject
        {
            public OtherSubject(string naturalKey)
                : base(naturalKey)
            {
            }

            protected internal OtherSubject()
            {
            }
        }

        private class SubjectCreated
        {
            public string NaturalKey { get; set; }
        }
    }
}
