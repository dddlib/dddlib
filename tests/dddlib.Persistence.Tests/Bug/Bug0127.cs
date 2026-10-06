using dddlib.Persistence.SqlServer;
using dddlib.Tests.Support;

namespace dddlib.Persistence.Tests.Bug;

// https://github.com/dddlib/dddlib/issues/127
// A repository must serve more than one aggregate root type, including a type that inherits another.
public class Bug0127 : SqlServerIntegration
{
    [Test]
    public async Task ShouldWorkForMultipleAggregateRootTypes()
    {
        var repository = new SqlServerEventStoreRepository(this.ConnectionString);
        var subject = new Subject("subject");
        var otherSubject = new OtherSubject("otherSubject");
        await repository.SaveAsync(subject);
        await repository.SaveAsync(otherSubject);

        repository = new SqlServerEventStoreRepository(this.ConnectionString);
        var sameSubject = await repository.LoadAsync<Subject>(subject.NaturalKey!);
        Func<Task> action = () => repository.LoadAsync<OtherSubject>(otherSubject.NaturalKey!);

        await Assert.That(sameSubject).IsEqualTo(subject);
        await Assert.That(action).ThrowsNothing();
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
        public string? NaturalKey { get; private set; }

        private void Handle(SubjectCreated @event) => this.NaturalKey = @event.NaturalKey;
    }

    private class OtherSubject : Subject
    {
        public OtherSubject(string naturalKey)
            : base(naturalKey)
        {
        }

        internal OtherSubject()
        {
        }
    }

    private sealed class SubjectCreated
    {
        public string? NaturalKey { get; set; }
    }
}
