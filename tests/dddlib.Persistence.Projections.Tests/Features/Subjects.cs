using dddlib.Configuration;
using dddlib.Runtime;

namespace dddlib.Persistence.Projections.Tests.Features;

// The model the projection scenarios share: an aggregate root with two events, a view of it, and projections over
// them that record what they applied and can be told to fail. The bootstrapper is found by assembly scanning, since
// these types are not nested in a scenario.
public class Subject : AggregateRoot
{
    public Subject(string id)
    {
        this.Apply(new NewSubject { Id = id });
    }

    internal Subject()
    {
    }

    [NaturalKey]
    public string? Id { get; private set; }

    public string? Name { get; private set; }

    public void Rename(string name) => this.Apply(new SubjectRenamed { Id = this.Id, Name = name });

    private void Handle(NewSubject @event) => this.Id = @event.Id;

    private void Handle(SubjectRenamed @event) => this.Name = @event.Name;
}

public class NewSubject
{
    public string? Id { get; set; }
}

public class SubjectRenamed
{
    public string? Id { get; set; }

    public string? Name { get; set; }
}

public sealed record SubjectView(string Id, string? Name, int Events);

public sealed class SubjectProjection : Projection<string, SubjectView>
{
    private readonly Lock sync = new();
    private readonly List<long> applied = [];
    private bool failed;

    public SubjectProjection()
        : base("subjects")
    {
        this.When<NewSubject>(async (envelope, @event, views, cancellationToken) =>
        {
            this.Record(envelope.SequenceNumber);
            this.ThrowIfPoisoned(@event.Id!);
            await views.AddOrUpdateAsync(@event.Id!, new SubjectView(@event.Id!, null, 1), cancellationToken);
        });

        this.When<SubjectRenamed>(async (envelope, @event, views, cancellationToken) =>
        {
            this.Record(envelope.SequenceNumber);
            var view = await views.GetAsync(@event.Id!, cancellationToken) ?? throw new InvalidOperationException("The view must exist.");
            await views.AddOrUpdateAsync(@event.Id!, view with { Name = @event.Name, Events = view.Events + 1 }, cancellationToken);
        });
    }

    /// <summary>
    /// Gets the sequence numbers the handlers were given, in order, including the ones of pages that failed.
    /// </summary>
    public IReadOnlyList<long> Applied
    {
        get
        {
            lock (this.sync)
            {
                return [.. this.applied];
            }
        }
    }

    public string? FailOn { get; init; }

    public bool FailOnce { get; init; }

    private void Record(long sequenceNumber)
    {
        lock (this.sync)
        {
            this.applied.Add(sequenceNumber);
        }
    }

    private void ThrowIfPoisoned(string id)
    {
        if (id == this.FailOn && !(this.FailOnce && this.failed))
        {
            this.failed = true;
            throw new InvalidOperationException("poisoned");
        }
    }
}

public sealed class NewSubjectsOnlyProjection : Projection<string, SubjectView>
{
    public NewSubjectsOnlyProjection()
        : base("new-subjects")
    {
        this.When<NewSubject>((envelope, @event, views, cancellationToken) =>
        {
            this.Applied.Add(envelope.SequenceNumber);
            return views.AddOrUpdateAsync(@event.Id!, new SubjectView(@event.Id!, null, 1), cancellationToken);
        });
    }

    public List<long> Applied { get; } = [];
}

public sealed class CatchAllProjection : Projection<string, SubjectView>
{
    public CatchAllProjection()
        : base("everything")
    {
        this.When<NewSubject>((envelope, _, _, _) =>
        {
            this.Exact.Add(envelope.SequenceNumber);
            return Task.CompletedTask;
        });

        this.When<object>((envelope, _, _, _) =>
        {
            this.CaughtAll.Add(envelope.SequenceNumber);
            return Task.CompletedTask;
        });
    }

    public List<long> Exact { get; } = [];

    public List<long> CaughtAll { get; } = [];
}

internal sealed class SubjectsBootstrapper : IBootstrapper
{
    public void Bootstrap(IConfiguration configure)
    {
        configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
    }
}
