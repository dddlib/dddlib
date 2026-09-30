# Aggregate Root Reconstitution

In **dddlib** reconstituting an aggregate root requires an uninitialized instance of the aggregate root to which the
saved state (a memento, an event stream, or both) is applied.

Add a `protected internal` parameterless constructor to the aggregate root:

```csharp
public partial class Vehicle : dddlib.AggregateRoot
{
    // used for reconstitution only
    protected internal Vehicle()
    {
    }

    public Vehicle(string registration)
    {
        this.Registration = registration;
    }

    [dddlib.NaturalKey]
    public string? Registration { get; private set; }
}
```

That is enough: the runtime (or the [source generator](source-generator.md), for a `partial` type) uses the
parameterless constructor as the reconstitution factory, whatever its accessibility.

Alternatively, or to override the constructor, configure reconstitution in the [bootstrapper](bootstrapper.md):

```csharp
internal sealed class Bootstrapper : dddlib.Configuration.IBootstrapper
{
    public void Bootstrap(dddlib.Configuration.IConfiguration configure)
    {
        configure.AggregateRoot<Vehicle>()
            .ToReconstituteUsing(() => new Vehicle());
    }
}
```

An aggregate root without a reconstitution factory cannot be persisted: the repositories throw a
`PersistenceException` whose message explains what to add. It also does not record applied events as uncommitted,
since there would be no way to load them back. The analyzer reports an aggregate root that has neither a
parameterless constructor nor a `ToReconstituteUsing` call (DDDLIB014) and offers to add the constructor.

Aggregate root reconstitution is never used without persistence. However, it should always be defined in order to
support [domain model reuse](concepts.md#domain-model-reuse).
