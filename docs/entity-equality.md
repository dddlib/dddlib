# Entity Equality

In **dddlib** entity equality is determined by the value of the natural key of the entity. The natural key can be
specified in the metadata of the entity class or in the [bootstrapper](bootstrapper.md) for the assembly.

Below is an entity modelling a (trivial) vehicle:

```csharp
public partial class Vehicle : dddlib.Entity
{
    public Vehicle(string registration)
    {
        this.Registration = registration;
    }

    // decorate the natural key property with the NaturalKey attribute
    [dddlib.NaturalKey]
    public string Registration { get; private set; }
}
```

Instances of this vehicle exhibit value equality:

```csharp
var vehicle = new Vehicle("W807ASB");
var otherVehicle = new Vehicle("W807ASB");

// these are equal
var areEqual = vehicle == otherVehicle;
```

It is also possible to specify the natural key in the [bootstrapper](bootstrapper.md). In that case it is not
necessary to decorate the property with the `NaturalKey` attribute.

```csharp
internal sealed class Bootstrapper : dddlib.Configuration.IBootstrapper
{
    public void Bootstrap(dddlib.Configuration.IConfiguration configure)
    {
        configure.Entity<Vehicle>()
            .ToUseNaturalKey(vehicle => vehicle.Registration);
    }
}
```

## Rules

- The natural key property must be public and declared on the entity type itself (or on a base type, which is then
  inherited). A subclass may declare its own natural key, which replaces the inherited one.
- An entity may declare at most one natural key. Declaring two is reported by the analyzer (DDDLIB001) and, at
  runtime, throws a `RuntimeException`. Declaring the same key in both the attribute and the bootstrapper is fine;
  declaring different ones is a `RuntimeException`.
- Two entities are equal when they are of the same runtime type and their natural key values are equal. Equality of
  the key values uses the key type's own equality, so a [value object](value-objects.md) key compares structurally
  and a `string` key compares ordinally.
- An entity without a natural key is only equal to itself.
