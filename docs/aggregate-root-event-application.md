# Aggregate Root Event Application

In **dddlib** it is possible to model a change in state as an event. This can make a model cleaner by separating the
business logic (whether something _can_ happen) from the change itself (when something _has_ happened). It also opens
up the possibility of [persisting the model as an event stream](persistence/event-sourcing-persistence.md).

Below is a conventional aggregate root modelling a (trivial) vehicle:

```csharp
public partial class Vehicle : dddlib.AggregateRoot
{
    public Vehicle(string registration)
    {
        // business logic
        if (string.IsNullOrEmpty(registration) ||
            !registration.StartsWith("J", System.StringComparison.OrdinalIgnoreCase))
        {
            throw new dddlib.BusinessException(
                $"The specified registration '{registration}' must start with a 'J'.");
        }

        // change in state
        this.Registration = registration;
    }

    [dddlib.NaturalKey]
    public string Registration { get; private set; }
}
```

This model can be re-written to make use of event application:

```csharp
public partial class Car : dddlib.AggregateRoot
{
    public Car(string registration)
    {
        // business logic
        if (string.IsNullOrEmpty(registration) ||
            !registration.StartsWith("J", System.StringComparison.OrdinalIgnoreCase))
        {
            throw new dddlib.BusinessException(
                $"The specified registration '{registration}' must start with a 'J'.");
        }

        this.Apply(new CarRegistered { Registration = registration });
    }

    [dddlib.NaturalKey]
    public string? Registration { get; private set; }

    private void Handle(CarRegistered @event)
    {
        // change in state
        this.Registration = @event.Registration;
    }
}

public class CarRegistered
{
    public string? Registration { get; set; }
}
```

In the re-written model the business logic is separated from the change in state. After the business logic has been
validated, `Apply` is called with an event that describes the change. Internally the event is dispatched to the
private `Handle` method, where the change in state is applied to the aggregate.

## Handler Rules

- A handler is a non-public instance method named `Handle` (case-insensitive) with exactly one parameter.
- The parameter type must be a class. An event is dispatched to the handler whose parameter type is exactly the
  event's runtime type; a handler for a base event type does not receive derived events.
- Handlers may be declared at any level of the aggregate root's class hierarchy. Each level dispatches its own
  declared handlers, so a base class handler and a subclass handler for the same event both run.
- Events must be classes. Applying a value type throws a `RuntimeException`.

The analyzers report a handler with a value-type parameter (DDDLIB002) and a public handler (DDDLIB003), since the
dispatcher would silently ignore both. For `partial` aggregate roots the dispatch code is
[generated at compile time](source-generator.md); otherwise the handlers are discovered by reflection once per type.
