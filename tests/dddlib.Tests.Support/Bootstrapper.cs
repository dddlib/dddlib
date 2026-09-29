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
            .ToUseEqualityComparer(new RegistrationEqualityComparer())
            .ToUseValueObjectSerializer(new RegistrationSerializer());
    }

    private sealed class RegistrationEqualityComparer : IEqualityComparer<Registration>
    {
        public bool Equals(Registration? x, Registration? y) =>
            x is null || y is null ? x is null && y is null : string.Equals(x.Number, y.Number, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(Registration obj) => StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Number);
    }

    private sealed class RegistrationSerializer : IValueObjectSerializer
    {
        public string Serialize(object valueObject) => ((Registration)valueObject).Number;

        public object Deserialize(string serializedValueObject) => new Registration(serializedValueObject);
    }
}
