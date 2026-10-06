# Aggregate Root Mementos

In **dddlib** the current state of an aggregate root may be persisted through a repository. Whilst the act of
persisting the state is external to the aggregate root, the representation of the state and its contents are the
responsibility of the aggregate root. It is always necessary to define this representation when using a
[memento repository](persistence/memento-persistence.md), and optionally when using an
[event store repository](persistence/event-sourcing-persistence.md), depending on whether
[snapshotting](persistence/event-sourcing-persistence.md#snapshotting) is desired.

The current state of an aggregate root is described by a memento. In the example below `GetState` and `SetState` are
overridden, and a private class named `Memento` represents the state.

```csharp
public partial class Car : dddlib.AggregateRoot
{
    // used for reconstitution only
    protected Car()
    {
    }

    public Car(string registration)
    {
        // business logic goes here...
        this.Registration = registration;
    }

    [dddlib.NaturalKey]
    public string? Registration { get; private set; }

    // used for memento reconstitution or snapshot loading
    protected override object? GetState()
    {
        return new Memento { Registration = this.Registration };
    }

    // used for memento persistence or snapshot saving
    protected override void SetState(object memento)
    {
        var car = (Memento)memento;
        this.Registration = car.Registration;
    }

    // an object used to describe the state of the aggregate root
    private sealed class Memento
    {
        public string? Registration { get; set; }
    }
}
```

The memento is serialized as JSON by the persistence layer (see [serialization](persistence/serialization.md)), so
it should be a plain object with public settable properties. A memento type may be private: it is found again by
name when loading.

`GetState` returns null by default and `SetState` throws a `RuntimeException` by default, so an aggregate root that
does not override them can only be persisted through an event store repository without snapshots. Overriding only
one of the two is reported by the analyzer (DDDLIB013), which offers to add the other as a stub.
