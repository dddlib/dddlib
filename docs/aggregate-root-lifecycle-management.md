# Aggregate Root Lifecycle Management

In **dddlib** aggregate roots _are_ entities and, as such, use the underlying
[entity lifecycle management](entity-lifecycle-management.md) implementation.

However, if the aggregate root makes use of [event application](aggregate-root-event-application.md) then
responsibility for lifecycle management can be deferred to the base class. In the example below, calling `Scrap` more
than once throws a [business exception](business-exceptions.md): in this mode the aggregate root tracks changes as
events and refuses to apply any change once the lifecycle has ended.

```csharp
public partial class Car : dddlib.AggregateRoot
{
    public Car(string registration)
    {
        this.Apply(new CarRegistered { Registration = registration });
    }

    [dddlib.NaturalKey]
    public string? Registration { get; private set; }

    public void Scrap()
    {
        this.Apply(new CarScrapped { Registration = this.Registration });
    }

    private void Handle(CarRegistered @event)
    {
        this.Registration = @event.Registration;
    }

    private void Handle(CarScrapped @event)
    {
        this.EndLifecycle();
    }
}

public class CarRegistered
{
    public string? Registration { get; set; }
}

public class CarScrapped
{
    public string? Registration { get; set; }
}
```

The message of the exception names the aggregate root, its natural key and the event that was refused, for example
`Cannot apply 'CarScrapped' to 'W807ASB' because that 'Car' no longer exists in the system.`
