using dddlib.Runtime;
using dddlib.Sdk;
using dddlib.Sdk.Generated;
using dddlib.Tests.Support;

namespace dddlib.Tests.Unit;

// Proves the runtime uses the source-generated metadata for partial types and reflection for the rest.
public partial class GeneratedMetadataTests
{
    [Test]
    public async Task PartialAggregateRootHasGeneratedMetadata()
    {
        var metadata = GeneratedMetadata.TryGet<IGeneratedAggregateRootMetadata>(typeof(Vehicle));

        await Assert.That(metadata).IsNotNull();
        await Assert.That(metadata!.NaturalKeyPropertyName).IsEqualTo(nameof(Vehicle.Registration));
        await Assert.That(metadata.NaturalKeyPropertyType).IsEqualTo(typeof(Registration));
        await Assert.That(metadata.UninitializedFactory).IsTypeOf<Func<Vehicle>>();
    }

    [Test]
    public async Task NonPartialTypeHasNoGeneratedMetadata()
    {
        await Assert.That(GeneratedMetadata.TryGet<IGeneratedEntityMetadata>(typeof(Plain))).IsNull();
    }

    [Test]
    public async Task RuntimeUsesGeneratedDispatcherForPartialAggregateRoot()
    {
        using (new Application())
        {
            var runtimeType = Application.Current.GetAggregateRootType(typeof(Vehicle));

            await Assert.That(runtimeType.EventDispatcher).IsAssignableTo<IGeneratedAggregateRootMetadata>();
            await Assert.That(runtimeType.NaturalKey).IsNotNull();
            await Assert.That(runtimeType.UninitializedFactory).IsNotNull();
        }
    }

    [Test]
    public async Task RuntimeUsesReflectionDispatcherForNonPartialAggregateRoot()
    {
        using (new Application())
        {
            var runtimeType = Application.Current.GetAggregateRootType(typeof(PlainAggregate));

            await Assert.That(runtimeType.EventDispatcher).IsTypeOf<DefaultEventDispatcher>();
        }
    }

    [Test]
    public async Task GeneratedDispatcherAppliesEventsExactlyLikeReflection()
    {
        var registration = new Registration("abc", new AcceptingRegistrationService());

        var vehicle = new Vehicle(registration);
        var subclass = new Lorry(registration);

        await Assert.That(vehicle.Registration).IsEqualTo(registration);
        await Assert.That(subclass.Registration).IsEqualTo(registration);
        await Assert.That(subclass.Loads).IsEqualTo(1);
    }

    [Test]
    public async Task PartialValueObjectUsesGeneratedComparer()
    {
        using (new Application())
        {
            var runtimeType = Application.Current.GetValueObjectType(typeof(Money));

            await Assert.That(runtimeType.EqualityComparer).IsAssignableTo<IGeneratedValueObjectMetadata>();
            await Assert.That(new Money { Amount = 1, Currency = "AUD" }).IsEqualTo(new Money { Amount = 1, Currency = "AUD" });
            await Assert.That(new Money { Amount = 1, Currency = "AUD" }).IsNotEqualTo(new Money { Amount = 2, Currency = "AUD" });
            await Assert.That(new Money { Amount = 1, Currency = "AUD" }.GetHashCode()).IsEqualTo(new Money { Amount = 1, Currency = "AUD" }.GetHashCode());
        }
    }

    [Test]
    public async Task BootstrapperIsRegisteredAtModuleInitialization()
    {
        var registered = BootstrapperRegistry.TryGet(typeof(Vehicle).Assembly, out var bootstrapperType);

        await Assert.That(registered).IsTrue();
        await Assert.That(bootstrapperType?.Name).IsEqualTo("Bootstrapper");
    }

    public class Plain : Entity
    {
    }

    public class PlainAggregate : AggregateRoot
    {
    }

    // A non-partial subclass of a partial aggregate root: the generated dispatcher handles the Vehicle level and
    // reflection handles the Lorry level.
    public class Lorry : Vehicle
    {
        public Lorry(Registration registration)
            : base(registration)
        {
            this.Apply(new LorryLoaded());
        }

        public int Loads { get; private set; }

        private void Handle(LorryLoaded @event) => this.Loads++;
    }

    public class LorryLoaded
    {
    }

    public partial class Money : ValueObject<Money>
    {
        public decimal Amount { get; set; }

        public string? Currency { get; set; }
    }

    private sealed class AcceptingRegistrationService : IRegistrationService
    {
        public bool ConfirmValid(string registrationNumber) => true;
    }
}
