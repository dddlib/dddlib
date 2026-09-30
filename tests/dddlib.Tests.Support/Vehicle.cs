using dddlib.Runtime;

namespace dddlib.Tests.Support;

public partial class Vehicle : AggregateRoot
{
    public Vehicle(Registration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        this.Apply(new NewVehicle { RegistrationNumber = registration.Number });
    }

    protected internal Vehicle()
    {
    }

    [NaturalKey]
    public Registration? Registration { get; private set; }

    private void Handle(NewVehicle @event)
    {
        if (this.Registration is not null)
        {
#pragma warning disable DDDLIB011 // a test probe, not model logic: it fails any scenario that dispatches an event twice
            throw new RuntimeException("Event processed twice!");
#pragma warning restore DDDLIB011
        }

        this.Registration = new Registration(@event.RegistrationNumber!);
    }
}
