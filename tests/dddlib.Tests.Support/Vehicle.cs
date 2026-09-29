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
            throw new RuntimeException("Event processed twice!");
        }

        this.Registration = new Registration(@event.RegistrationNumber!);
    }
}
