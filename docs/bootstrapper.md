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

Using a mapping that has not been registered throws a `RuntimeException` that names the missing mapping.
