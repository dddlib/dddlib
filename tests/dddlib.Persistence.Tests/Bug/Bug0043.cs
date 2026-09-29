using dddlib.Persistence.Memory;

namespace dddlib.Persistence.Tests.Bug;

// https://github.com/dddlib/dddlib/issues/43
// Loading with a natural key of the wrong type must fail with an argument exception rather than a lookup miss.
// The legacy test used the memento-based repository; both repositories behave the same.
public class Bug0043
{
    [Test]
    public async Task ShouldThrow()
    {
        var repository = new MemoryEventStoreRepository();
        var registration = new Registraion { Number = "abc" };
        var car = new Car(registration);
        await repository.SaveAsync(car);

        Func<Task> action = () => repository.LoadAsync<Car>(registration);

        await Assert.That(action).Throws<ArgumentException>();
    }

    [Test]
    public async Task ShouldThrowForMemoryRepository()
    {
        var repository = new MemoryRepository<Car>();
        var registration = new Registraion { Number = "abc" };
        var car = new Car(registration);
        await repository.SaveAsync(car);

        Func<Task> action = () => repository.LoadAsync(registration);

        await Assert.That(action).Throws<ArgumentException>();
    }

    public class Car : AggregateRoot
    {
        public Car(Registraion registration)
        {
            ArgumentNullException.ThrowIfNull(registration);

            this.Apply(new NewCar { RegistrationNumber = registration.Number });
        }

        internal Car()
        {
        }

        [NaturalKey]
        public string? RegistrationNumber { get; private set; }

        private void Handle(NewCar @event) => this.RegistrationNumber = @event.RegistrationNumber;

        protected override object? GetState() => this.RegistrationNumber;

        protected override void SetState(object memento) => this.RegistrationNumber = memento.ToString();
    }

    public class NewCar
    {
        public string? RegistrationNumber { get; set; }
    }

    public class Registraion : ValueObject<Registraion>
    {
        public string? Number { get; set; }
    }
}
