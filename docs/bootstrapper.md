# Bootstrapper

In **dddlib** it is not always possible to specify every feature you want to use in the model itself. In that case
create a bootstrapper by implementing `dddlib.Configuration.IBootstrapper`. Only one bootstrapper is allowed per
assembly, and it needs a public parameterless constructor. It is recommended that the bootstrapper is declared
`internal`.

Below is an example of a bootstrapper:

```csharp
internal sealed class Bootstrapper : dddlib.Configuration.IBootstrapper
{
    public void Bootstrap(dddlib.Configuration.IConfiguration configure)
    {
        configure.AggregateRoot<Car>()
            .ToReconstituteUsing(() => new Car())
            .ToUseNaturalKey(car => car.Registration);

        configure.Entity<Wheel>()
            .ToUseNaturalKey(wheel => wheel.Id)
            .ToMapToEvent<WheelFitted>(
                (wheel, @event) => @event.WheelId = wheel.Id,
                @event => new Wheel(@event.WheelId));

        configure.ValueObject<Registration>()
            .ToUseEqualityComparer(new RegistrationEqualityComparer())
            .ToUseValueObjectSerializer(new RegistrationSerializer())
            .ToMapToEvent<CarRegistered>(
                (registration, @event) => @event.Registration = registration.Number);
    }
}
```

The bootstrapper can configure every **dddlib** feature for any part of the model:

| Method | Applies to | Purpose |
|---|---|---|
| `ToUseNaturalKey` | aggregate roots, entities | The property used for [equality](entity-equality.md) |
| `ToReconstituteUsing` | aggregate roots | The [reconstitution](aggregate-root-reconstitution.md) factory |
| `ToUseEqualityComparer` | value objects | Custom [equality](value-object-equality.md) |
| `ToUseValueObjectSerializer` | value objects | Custom [serialization](value-object-serialization.md) |
| `ToMapToEvent` | entities, value objects | Mapping to (and optionally from) an event, used through `Map` inside an aggregate root |

## How the bootstrapper is found

The bootstrapper is invoked lazily, the first time each domain type in its assembly is used, and only the
configuration for that type takes effect on that call. The [source generator](source-generator.md) registers the
assembly's bootstrapper at compile time, so no assembly scanning happens at runtime; assemblies compiled without the
generator are scanned once. The analyzers report more than one bootstrapper in an assembly (DDDLIB005) and a
bootstrapper without a public parameterless constructor (DDDLIB006).

## Mapping

`ToMapToEvent` registers a forward mapping (entity or value object to event) and, optionally, a reverse mapping. Inside
an aggregate root these are used through the `Map` property:

```csharp
public void Register(Registration registration)
{
    this.Apply(this.Map.ValueObject(registration).ToEvent<CarRegistered>());
}

private void Handle(CarRegistered @event)
{
    this.Registration = this.Map.Event(@event).ToValueObject<Registration>();
}
```

Using a mapping that has not been registered throws a `RuntimeException` that names the missing mapping. The
analyzer reports such a use at compile time (DDDLIB020).

## What the analyzers read from the bootstrapper

Several diagnostics depend on what the bootstrapper configures: a missing reconstitution factory (DDDLIB014), a
missing natural key (DDDLIB015), a natural key that does not round-trip (DDDLIB017), a missing mapping (DDDLIB020),
and the value object rules that a custom comparer switches off (DDDLIB004, DDDLIB019). The analyzers read the body of
the `Bootstrap` method to find out, and understand exactly one shape: statements that start at the `configure`
parameter and chain the configuration methods, as in every example on this page.

```csharp
configure.AggregateRoot<Car>()
    .ToReconstituteUsing(() => new Car())
    .ToUseNaturalKey(car => car.Registration);
```

Such statements may sit inside `if` blocks and loops. An assembly without a bootstrapper is read as configuring
nothing. The analyzers give up, and report none of the diagnostics above, when configuration may happen somewhere
they cannot follow:

- the assembly has more than one bootstrapper (DDDLIB005 is reported instead);
- `configure` is passed to another method, assigned, or captured in anything but a call of `AggregateRoot<T>()`,
  `Entity<T>()` or `ValueObject<T>()`;
- the result of one of those calls is stored or passed on rather than chained;
- any other method in the assembly takes an `IConfiguration`, which is how classes called by a custom
  `IBootstrapperProvider` look.

The runtime checks remain in every case; the diagnostics only bring them forward.
