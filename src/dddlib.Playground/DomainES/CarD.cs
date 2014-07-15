namespace dddlib.Playground.DomainES
{
    using System;

    public class CarD : AggregateRoot
    {
        // NOTE (Cameron): For reconstitution prior to setting state.
        internal CarD()
        {
        }

        public CarD(Registration registration)
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

        public int MaxRecordedSpeed { get; private set; }

        public int MinRecordedSpeed { get; private set; }

        public void PassedThroughSpeedTrapAt(int kmph)
        {
            this.Apply(new CarPassedThroughSpeedTrap { RegistrationNumber = this.Registration.Number, Speed = kmph });
        }

        private void Handle(CarEnteredIntoSystem @event)
        {
            this.Registration = new Registration(@event.RegistrationNumber);
        }

        private void Handle(CarPassedThroughSpeedTrap @event)
        {
            this.MaxRecordedSpeed = Math.Max(this.MaxRecordedSpeed, @event.Speed);
            this.MinRecordedSpeed = this.MinRecordedSpeed == 0 ? @event.Speed : Math.Min(this.MinRecordedSpeed, @event.Speed); // HACK (Cameron): Massive hack.
        }
    }
}
