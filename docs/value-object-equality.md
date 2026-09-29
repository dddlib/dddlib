# Value Object Equality

In **dddlib** value object equality is determined by the values of the public properties of the value object. If
custom value object equality is required it can be specified in the [bootstrapper](bootstrapper.md) for the assembly.

Below is a value object modelling a (trivial) vehicle registration:

```csharp
public sealed partial class Registration : dddlib.ValueObject<Registration>
{
    public Registration(string number)
    {
        this.Number = number;
    }

    public string Number { get; }
}
```

Instances of this registration exhibit value equality:

```csharp
var registration = new Registration("W807ASB");
var otherRegistration = new Registration("W807ASB");

// these are equal
var areEqual = registration == otherRegistration;
```

## Default Equality Rules

- All public readable properties take part, declared and inherited.
- A property whose type is enumerable (other than `string`) compares element by element, so a `List<string>` and a
  `string[]` with the same elements are equal. Hash codes follow the same rule.
- Every other property compares with `EqualityComparer<T>.Default`, so nested value objects and types that override
  `Equals` behave as expected.
- Private fields never take part.
- Two value objects of different runtime types are never equal.
- A value object with no public properties throws a `RuntimeException` on construction under the default comparer,
  since no two instances could ever be equal. The analyzer reports this as DDDLIB004.

For a `partial` value object the comparer is [generated at compile time](source-generator.md); otherwise it is built
by reflection once per type.

## Custom Value Object Equality

If a value object requires custom equality, specify an equality comparer in the [bootstrapper](bootstrapper.md).
For example, to make the registration case-insensitive:

```csharp
internal sealed class Bootstrapper : dddlib.Configuration.IBootstrapper
{
    public void Bootstrap(dddlib.Configuration.IConfiguration configure)
    {
        configure.ValueObject<Registration>()
            .ToUseEqualityComparer(new RegistrationEqualityComparer());
    }
}

internal sealed class RegistrationEqualityComparer : IEqualityComparer<Registration>
{
    public bool Equals(Registration? x, Registration? y) =>
        string.Equals(x?.Number, y?.Number, StringComparison.OrdinalIgnoreCase);

    public int GetHashCode(Registration obj) =>
        StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Number);
}
```

Instances of this registration now exhibit case-insensitive value equality:

```csharp
var registration = new Registration("W807ASB");
var otherRegistration = new Registration("w807asb"); // different case

// these are equal
var areEqual = registration == otherRegistration;
```

The comparer must be configured before the first instance of the value object is created; configuring it afterwards
throws.
