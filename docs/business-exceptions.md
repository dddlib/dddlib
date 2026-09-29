# Business Exceptions

In **dddlib** when business logic criteria are not met, a business exception should be thrown.

> The model author should take care that the exception message carries enough information for a business user of
> the system to understand it.

Below is a value object modelling a (trivial) vehicle registration for Jersey, where all vehicle registrations begin
with a 'J':

```csharp
public sealed partial class Registration : dddlib.ValueObject<Registration>
{
    public Registration(string number)
    {
        if (string.IsNullOrEmpty(number) ||
            !number.StartsWith("J", System.StringComparison.OrdinalIgnoreCase))
        {
            throw new dddlib.BusinessException(
                $"The specified registration '{number}' must start with a 'J'.");
        }

        this.Number = number;
    }

    public string Number { get; }
}
```

It is not possible to instantiate this registration unless it starts with the letter 'J':

```csharp
// this will throw a BusinessException
var registration = new Registration("W807ASB");
```

`BusinessException` is distinct from `dddlib.Runtime.RuntimeException`, which signals a problem with how the model is
defined or configured rather than a violation of a business rule. Runtime exception messages end with a "To fix this
issue" section and a link to the relevant page of this documentation.
