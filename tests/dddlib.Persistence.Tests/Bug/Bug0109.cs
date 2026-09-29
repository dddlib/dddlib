using dddlib.Persistence.Memory;
using dddlib.Persistence.SqlServer;
using dddlib.Tests.Support;

namespace dddlib.Persistence.Tests.Bug;

// https://github.com/dddlib/dddlib/issues/109
// Saving a stale instance after the aggregate root was destroyed and saved is a concurrency error.
// The legacy ShouldThrowForMemoryRepository case covered the dropped memento-based repository;
// The SQL Server case runs against a per-class database created by the fixture.
public class Bug0109 : SqlServerIntegration
{
    [Test]
    public async Task ShouldThrowForMemoryEventStoreRepository()
    {
        var repository = new MemoryEventStoreRepository();
        var naturalKey = "key";
        var subject = new EventBasedSubject(naturalKey);
        await repository.SaveAsync(subject);
        var sameSubject = await repository.LoadAsync<EventBasedSubject>(subject.NaturalKey!);
        subject.Destroy();
        await repository.SaveAsync(subject);
        sameSubject.Change();

        var action = () => repository.SaveAsync(sameSubject);

        await Assert.That(action).Throws<ConcurrencyException>();
    }

    [Test]
    public async Task ShouldThrowForSqlServerEventStoreRepository()
    {
        var repository = new SqlServerEventStoreRepository(this.ConnectionString);
        var naturalKey = "key";
        var subject = new EventBasedSubject(naturalKey);
        await repository.SaveAsync(subject);
        var sameSubject = await repository.LoadAsync<EventBasedSubject>(subject.NaturalKey!);
        subject.Destroy();
        await repository.SaveAsync(subject);
        sameSubject.Change();

        var action = () => repository.SaveAsync(sameSubject);

        await Assert.That(action).Throws<ConcurrencyException>();
    }

    private sealed class EventBasedSubject : AggregateRoot
    {
        public EventBasedSubject(string naturalKey)
        {
            this.Apply(new SubjectCreated { NaturalKey = naturalKey });
        }

        internal EventBasedSubject()
        {
        }

        [NaturalKey]
        public string? NaturalKey { get; private set; }

        public void Change() => this.Apply(new SubjectChanged { NaturalKey = this.NaturalKey });

        public void Destroy() => this.Apply(new SubjectDestroyed { NaturalKey = this.NaturalKey });

        private void Handle(SubjectCreated @event) => this.NaturalKey = @event.NaturalKey;

        private void Handle(SubjectDestroyed @event) => this.EndLifecycle();
    }

    private sealed class SubjectCreated
    {
        public string? NaturalKey { get; set; }
    }

    private sealed class SubjectChanged
    {
        public string? NaturalKey { get; set; }
    }

    private sealed class SubjectDestroyed
    {
        public string? NaturalKey { get; set; }
    }
}
