using dddlib.Configuration;
using dddlib.Runtime;

namespace dddlib.Tests.Support;

internal sealed class Bootstrapper : IBootstrapper
{
    public void Bootstrap(IConfiguration configure)
    {
        configure.Entity<Vehicle>()
            .ToUseNaturalKey(vehicle => vehicle.Registration);

        configure.ValueObject<Registration>()
            .ToUseValueObjectSerializer(new RegistrationSerializer());
    }

    private sealed class RegistrationSerializer : IValueObjectSerializer
    {
        public string Serialize(object valueObject) => ((Registration)valueObject).Number;

        public object Deserialize(string serializedValueObject) => new Registration(serializedValueObject);
    }
}
