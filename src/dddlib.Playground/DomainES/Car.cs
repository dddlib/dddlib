namespace dddlib.Playground.DomainES
{
    public class Car : AggregateRoot
    {
        // NOTE (Cameron): For reconstitution prior to setting state. Required for persistence by dddlib.dll.
        internal Car()
        {
        }

        public Car(Registration registration)
        {
            Guard.Against.Null(() => registration);

            if (registration.Number.StartsWith("P"))
            {
                throw new BusinessException("The car is an export and cannot be driven here.");
            }

            this.Apply(new CarEnteredIntoSystem { RegistrationNumber = registration.Number });
        }

        [NaturalKey]
        public Registration Registration { get; private set; }

        public void PassedThroughSpeedTrapAt(int kmph)
        {
            this.Apply(new CarPassedThroughSpeedTrap { RegistrationNumber = this.Registration.Number, Speed = kmph });
        }

        private void Handle(CarEnteredIntoSystem @event)
        {
            this.Registration = new Registration(@event.RegistrationNumber);
        }
    }

    public class CarEnteredIntoSystem
    {
        public string RegistrationNumber { get; set; }
    }

    public class CarPassedThroughSpeedTrap
    {
        public string RegistrationNumber { get; set; }

        public int Speed { get; set; }
    }
}
