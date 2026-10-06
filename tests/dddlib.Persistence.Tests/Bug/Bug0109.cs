using dddlib.Persistence.Memory;
using dddlib.Persistence.SqlServer;
using dddlib.Tests.Support;

namespace dddlib.Persistence.Tests.Bug;

// https://github.com/dddlib/dddlib/issues/109
// Saving a stale instance after the aggregate root was destroyed and saved is a concurrency error.
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

    [Test]
    public async Task ShouldThrowForMemoryRepository()
    {
        var repository = new MemoryRepository<ConventionalSubject>();
        await ShouldThrowForRepository(repository);
    }

    [Test]
    public async Task ShouldThrowForSqlServerMementoRepository()
    {
        var repository = new SqlServerMementoRepository<ConventionalSubject>(this.ConnectionString);
        await ShouldThrowForRepository(repository);
    }

    private static async Task ShouldThrowForRepository(IRepository<ConventionalSubject> repository)
    {
        var naturalKey = "key";
        var subject = new ConventionalSubject(naturalKey);
        await repository.SaveAsync(subject);
        var sameSubject = await repository.LoadAsync(subject.NaturalKey!);
        subject.Destroy();
        await repository.SaveAsync(subject);

        var action = () => repository.SaveAsync(sameSubject);

        await Assert.That(action).Throws<ConcurrencyException>();
    }

    private class ConventionalSubject : AggregateRoot
    {
        public ConventionalSubject(string naturalKey)
        {
            this.NaturalKey = naturalKey;
        }

        internal ConventionalSubject()
        {
        }

        [NaturalKey]
        public string? NaturalKey { get; private set; }

        public void Destroy() => this.EndLifecycle();

        protected override object? GetState() => new Memento { NaturalKey = this.NaturalKey };

        protected override void SetState(object memento) => this.NaturalKey = ((Memento)memento).NaturalKey;

        public sealed class Memento
        {
            public string? NaturalKey { get; set; }
        }
    }

    private class EventBasedSubject : AggregateRoot
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

        private void Handle(SubjectChanged @event) => this.NaturalKey = @event.NaturalKey;

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
