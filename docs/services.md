# Services

> Sometimes, it just isn't a thing.
> In some cases, the clearest and most pragmatic design includes operations that do not conceptually belong to any
> object. Rather than force the issue, we can follow the natural contours of the problem space and include _services_
> explicitly in the model.
> _Eric Evans, Domain-driven Design: Tackling Complexity in the Heart of Software_

In **dddlib** there are no underlying constraints on service implementation, only the suggested
[guidelines](guidelines.md) for service design. A common pattern is to pass a service into a value object or aggregate
root constructor for validation, as the test model does with `IRegistrationService`:

```csharp
public sealed partial class Registration : dddlib.ValueObject<Registration>
{
    public Registration(string number, IRegistrationService registrationService)
    {
        if (!registrationService.ConfirmValid(number))
        {
            throw new dddlib.BusinessException($"The specified registration number '{number}' is invalid.");
        }

        this.Number = number;
    }

    public string Number { get; }
}
```
