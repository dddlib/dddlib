using dddlib.Configuration;
using dddlib.Runtime;
using dddlib.Tests.Support;

namespace dddlib.Tests.Features.Generated;

// As someone who uses dddlib
// In order to compare aggregate roots
// I need aggregate roots to be equal when their natural keys are equal
public abstract partial class AggregateRootEquality : Feature
{
    public sealed partial class UndefinedNaturalKeySelector : AggregateRootEquality
    {
        [Test]
        public async Task Scenario()
        {
            // Given an aggregate root with an undefined natural key selector
            // When two instances of that aggregate root are instantiated
            var instance1 = new Subject();
            var instance2 = new Subject();

            // Then the first instance is not equal to the second instance
            await Assert.That(instance1).IsNotEqualTo(instance2);
        }

        public partial class Subject : AggregateRoot
        {
        }
    }

    public sealed partial class NaturalKeySelectorDefinedInMetadata : AggregateRootEquality
    {
        [Test]
        public async Task Scenario()
        {
            // Given an aggregate root with a natural key selector defined in metadata
            // And a natural key value
            var naturalKey = "key";

            // When two instances of that aggregate root are instantiated with that natural key value assigned
            var instance1 = new Subject { NaturalKey = naturalKey };
            var instance2 = new Subject { NaturalKey = naturalKey };

            // Then the first instance is equal to the second instance
            await Assert.That(instance1).IsEqualTo(instance2);
        }

        public partial class Subject : AggregateRoot
        {
            [NaturalKey]
            public string? NaturalKey { get; set; }
        }
    }

    public sealed partial class NaturalKeySelectorDefinedInBootstrapper : AggregateRootEquality
    {
        [Test]
        public async Task Scenario()
        {
            // Given an aggregate root with a natural key selector defined in the bootstrapper
            // And a natural key value
            var naturalKey = "key";

            // When two instances of that aggregate root are instantiated with that natural key value assigned
            var instance1 = new Subject { NaturalKey = naturalKey };
            var instance2 = new Subject { NaturalKey = naturalKey };

            // Then the first instance is equal to the second instance
            await Assert.That(instance1).IsEqualTo(instance2);
        }

        public partial class Subject : AggregateRoot
        {
            public string? NaturalKey { get; set; }
        }

        private sealed partial class BootStrapper : IBootstrap<Subject>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.AggregateRoot<Subject>().ToUseNaturalKey(subject => subject.NaturalKey);
            }
        }
    }

    public sealed partial class NonConflictingNaturalKeySelectors : AggregateRootEquality
    {
        [Test]
        public async Task Scenario()
        {
            // Given an aggregate root with non-conflicting natural key selectors defined in both metadata and the bootstrapper
            // And a natural key value
            var naturalKey = "key";

            // When two instances of that aggregate root are instantiated with that natural key value assigned
            var instance1 = new Subject { NaturalKey = naturalKey };
            var instance2 = new Subject { NaturalKey = naturalKey };

            // Then the first instance is equal to the second instance
            await Assert.That(instance1).IsEqualTo(instance2);
        }

        public partial class Subject : AggregateRoot
        {
            [NaturalKey]
            public string? NaturalKey { get; set; }
        }

        private sealed partial class BootStrapper : IBootstrap<Subject>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.AggregateRoot<Subject>().ToUseNaturalKey(subject => subject.NaturalKey);
            }
        }
    }

    public sealed partial class ConflictingNaturalKeySelectors : AggregateRootEquality
    {
        [Test]
        public async Task Scenario()
        {
            // Given an aggregate root with conflicting natural key selectors defined in both metadata and the bootstrapper
            // When an instance of that aggregate root is instantiated
            var action = () => { _ = new Subject(); };

            // Then a runtime exception should be thrown
            await Assert.That(action).Throws<RuntimeException>();
        }

        public partial class Subject : AggregateRoot
        {
            [NaturalKey]
            public string? FirstNaturalKey { get; set; }

            public string? SecondNaturalKey { get; set; }
        }

        private sealed partial class BootStrapper : IBootstrap<Subject>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.AggregateRoot<Subject>().ToUseNaturalKey(subject => subject.SecondNaturalKey);
            }
        }
    }

    public sealed partial class CaseSensitiveUndefinedEqualityComparer : AggregateRootEquality
    {
        [Test]
        public async Task Scenario()
        {
            // Given an aggregate root with a string natural key
            // And a natural key value
            var naturalKey = "key";

            // When two instances of that aggregate root are instantiated with natural key values that differ by case
            var instance1 = new Subject { NaturalKey = naturalKey.ToUpperInvariant() };
            var instance2 = new Subject { NaturalKey = naturalKey.ToLowerInvariant() };

            // Then the first instance is not equal to the second instance
            await Assert.That(instance1).IsNotEqualTo(instance2);
        }

        public partial class Subject : AggregateRoot
        {
            [NaturalKey]
            public string? NaturalKey { get; set; }
        }
    }

    public sealed partial class CaseInsensitiveEqualityComparerDefinedInBootstrapper : AggregateRootEquality
    {
        [Test]
        public async Task Scenario()
        {
            // Given an aggregate root whose natural key is a value object with case-insensitive equality
            // And a natural key value
            var naturalKey = "key";

            // When two instances of that aggregate root are instantiated with natural key values that differ by case
            var instance1 = new Subject { NaturalKey = new Key { Value = naturalKey.ToUpperInvariant() } };
            var instance2 = new Subject { NaturalKey = new Key { Value = naturalKey.ToLowerInvariant() } };

            // Then the first instance is equal to the second instance
            await Assert.That(instance1).IsEqualTo(instance2);
        }

        public partial class Key : ValueObject<Key>
        {
            public string? Value { get; set; }
        }

        public partial class Subject : AggregateRoot
        {
            public Key? NaturalKey { get; set; }
        }

        private sealed partial class BootStrapper : IBootstrap<Subject>, IBootstrap<Key>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.AggregateRoot<Subject>().ToUseNaturalKey(subject => subject.NaturalKey);
                configure.ValueObject<Key>().ToUseEqualityComparer(new KeyEqualityComparer());
            }

            private sealed partial class KeyEqualityComparer : IEqualityComparer<Key>
            {
                public bool Equals(Key? x, Key? y) => string.Equals(x?.Value, y?.Value, StringComparison.OrdinalIgnoreCase);

                public int GetHashCode(Key obj) => StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Value ?? string.Empty);
            }
        }
    }

    public sealed partial class CompositeNaturalKeyEqualityComparer : AggregateRootEquality
    {
        [Test]
        public async Task Scenario()
        {
            // Given an aggregate root whose natural key is a composite value object
            // And natural key component values
            var component1 = "key1";
            var component2 = "key2";

            // When two instances of that aggregate root are instantiated with equal composite natural keys
            var naturalKey1 = new NaturalKeyValue { Component1 = component1, Component2 = component2 };
            var naturalKey2 = new NaturalKeyValue { Component1 = component1, Component2 = component2 };
            var instance1 = new Subject { NaturalKey = naturalKey1 };
            var instance2 = new Subject { NaturalKey = naturalKey2 };

            // Then the first instance is equal to the second instance
            await Assert.That(instance1).IsEqualTo(instance2);
        }

        public partial class NaturalKeyValue : ValueObject<NaturalKeyValue>
        {
            public string? Component1 { get; set; }

            public string? Component2 { get; set; }
        }

        public partial class Subject : AggregateRoot
        {
            [NaturalKey]
            public NaturalKeyValue? NaturalKey { get; set; }
        }
    }

    public sealed partial class UndefinedNaturalKeySelectorWithInheritance : AggregateRootEquality
    {
        [Test]
        public async Task Scenario()
        {
            // Given an aggregate root with an undefined natural key selector
            // When two instances of a subclass of that aggregate root are instantiated
            var instance1 = new SuperSubject();
            var instance2 = new SuperSubject();

            // Then the first instance is not equal to the second instance
            await Assert.That(instance1).IsNotEqualTo(instance2);
        }

        public partial class Subject : AggregateRoot
        {
        }

        public partial class SuperSubject : Subject
        {
        }
    }

    public sealed partial class NaturalKeySelectorDefinedInBaseClass : AggregateRootEquality
    {
        [Test]
        public async Task Scenario()
        {
            // Given an aggregate root with a natural key selector defined in the base class
            // And a natural key value
            var naturalKey = "key";

            // When two instances of the subclass are instantiated with that natural key value assigned
            var instance1 = new SuperSubject { NaturalKey = naturalKey };
            var instance2 = new SuperSubject { NaturalKey = naturalKey };

            // Then the first instance is equal to the second instance
            await Assert.That(instance1).IsEqualTo(instance2);
        }

        public partial class Subject : AggregateRoot
        {
            [NaturalKey]
            public string? NaturalKey { get; set; }
        }

        public partial class SuperSubject : Subject
        {
        }
    }

    public sealed partial class NaturalKeySelectorDefinedISubclass : AggregateRootEquality
    {
        [Test]
        public async Task Scenario()
        {
            // Given an aggregate root with a natural key selector defined in the subclass
            // And a natural key value
            var naturalKey = "key";

            // When two instances of the subclass are instantiated with that natural key value assigned
            var instance1 = new SuperSubject { NaturalKey = naturalKey };
            var instance2 = new SuperSubject { NaturalKey = naturalKey };

            // Then the first instance is equal to the second instance
            await Assert.That(instance1).IsEqualTo(instance2);
        }

        public partial class Subject : AggregateRoot
        {
        }

        public partial class SuperSubject : Subject
        {
            [NaturalKey]
            public string? NaturalKey { get; set; }
        }
    }

    public sealed partial class NaturalKeySelectorDefinedInBothBaseClassAndSubclass : AggregateRootEquality
    {
        [Test]
        public async Task Scenario()
        {
            // Given an aggregate root with a natural key selector defined in the base class and the subclass
            // And a natural key value
            var naturalKey = "key";

            // When two instances of the subclass are instantiated with the subclass natural key value assigned
            var instance1 = new SuperSubject { NaturalKey = "unequalValue", NaturalKey2 = naturalKey };
            var instance2 = new SuperSubject { NaturalKey = "unequalValue2", NaturalKey2 = naturalKey };

            // Then the first instance is equal to the second instance
            await Assert.That(instance1).IsEqualTo(instance2);
        }

        public partial class Subject : AggregateRoot
        {
            [NaturalKey]
            public string? NaturalKey { get; set; }
        }

        public partial class SuperSubject : Subject
        {
            [NaturalKey]
            public string? NaturalKey2 { get; set; }
        }
    }

    public sealed partial class InheritedNaturalKeySelector : AggregateRootEquality
    {
        [Test]
        public async Task Scenario()
        {
            // Given a registration service
            var registrationService = new RegistrationService();

            // And a registration
            var registration = new Registration("abc", registrationService);

            // When two cars are instantiated with that registration
            var car = new Car(registration);
            var otherCar = new Car(registration);

            // Then the cars are equal
            await Assert.That(car).IsEqualTo(otherCar);
        }

        public partial class Car : Vehicle
        {
            public Car(Registration registration)
                : base(registration)
            {
            }
        }

        public partial class RegistrationService : IRegistrationService
        {
            public bool ConfirmValid(string registrationNumber) => true;
        }
    }

    public sealed partial class InheritedNaturalKeySelectorOveriddenInSubclass : AggregateRootEquality
    {
        [Test]
        public async Task Scenario()
        {
            // Given a registration service
            var registrationService = new RegistrationService();

            // And a registration
            var registration = new Registration("abc", registrationService);

            // And a registration with a number that differs by case
            var otherRegistration = new Registration("ABC", registrationService);

            // When two cars are instantiated with those registrations
            var car = new Car(registration);
            var otherCar = new Car(otherRegistration);

            // Then the cars are not equal (the subclass natural key is the case-sensitive registration number)
            await Assert.That(car).IsNotEqualTo(otherCar);
        }

        public partial class Car : Vehicle
        {
            public Car(Registration registration)
                : base(registration)
            {
                this.OtherRegistration = registration.Number;
            }

            [NaturalKey]
            public string OtherRegistration { get; set; }
        }

        public partial class RegistrationService : IRegistrationService
        {
            public bool ConfirmValid(string registrationNumber) => true;
        }
    }

    public sealed partial class InheritedNaturalKeySelectorOveriddenInBootstrapper : AggregateRootEquality
    {
        [Test]
        public async Task Scenario()
        {
            // Given a registration service
            var registrationService = new RegistrationService();

            // And a registration
            var registration = new Registration("abc", registrationService);

            // And a registration with a number that differs by case
            var otherRegistration = new Registration("ABC", registrationService);

            // When two cars are instantiated with those registrations
            var car = new Car(registration);
            var otherCar = new Car(otherRegistration);

            // Then the cars are not equal (the bootstrapper natural key is the case-sensitive registration number)
            await Assert.That(car).IsNotEqualTo(otherCar);
        }

        public partial class Car : Vehicle
        {
            public Car(Registration registration)
                : base(registration)
            {
                this.OtherRegistration = registration.Number;
            }

            public string OtherRegistration { get; set; }
        }

        public partial class RegistrationService : IRegistrationService
        {
            public bool ConfirmValid(string registrationNumber) => true;
        }

        private sealed partial class BootStrapper : IBootstrap<Car>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.AggregateRoot<Car>().ToUseNaturalKey(car => car.OtherRegistration);
            }
        }
    }
}
