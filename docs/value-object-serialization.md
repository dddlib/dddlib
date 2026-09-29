# Value Object Serialization

In **dddlib** [value objects](value-objects.md) are serialized when they are the natural key of an aggregate root
that is being persisted. Default serialization writes the public properties of the value object as JSON using
System.Text.Json. If custom serialization is required it can be specified in the [bootstrapper](bootstrapper.md).

> The serialized representation is _never_ used for equality operations.

To use the default serialization, the value object must have public properties for all of the characteristics that
represent its value, and either a single public constructor whose parameters match those properties by name, or a
public parameterless constructor and settable properties.

Below is a value object and its instantiation:

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

```csharp
var registration = new Registration("W807ASB");
```

The serialized representation of that registration looks like this:

```json
{"Number":"W807ASB"}
```

When a natural key does not round-trip through serialization with equality intact, the repository refuses to persist
the aggregate root with a `PersistenceException` that explains the three things to check.

## Custom Value Object Serialization

If a value object requires custom serialization, specify a serializer in the [bootstrapper](bootstrapper.md) in one
of two ways. Both examples make the registration serialize to a plain string.

Either implement `dddlib.Runtime.IValueObjectSerializer`:

```csharp
internal sealed class Bootstrapper : dddlib.Configuration.IBootstrapper
{
    public void Bootstrap(dddlib.Configuration.IConfiguration configure)
    {
        configure.ValueObject<Registration>()
            .ToUseValueObjectSerializer(new RegistrationSerializer());
    }
}

internal sealed class RegistrationSerializer : dddlib.Runtime.IValueObjectSerializer
{
    public string Serialize(object valueObject) => ((Registration)valueObject).Number;

    public object Deserialize(string serializedValueObject) => new Registration(serializedValueObject);
}
```

Or provide serialization delegates:

```csharp
internal sealed class Bootstrapper : dddlib.Configuration.IBootstrapper
{
    public void Bootstrap(dddlib.Configuration.IConfiguration configure)
    {
        configure.ValueObject<Registration>()
            .ToUseValueObjectSerializer(
                registration => registration.Number,
                number => new Registration(number));
    }
}
```

In both cases the serialized instance of that registration now looks like this:

```json
"W807ASB"
```
