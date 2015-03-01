namespace dddlib.Playground.DomainDDD
{
    using System;

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

            this.Registration = registration;
        }

        [NaturalKey]
        public Registration Registration { get; private set; }

        public int MaxRecordedSpeed { get; private set; }

        public int MinRecordedSpeed { get; private set; }

        public void PassedThroughSpeedTrapAt(int kmph)
        {
            this.MaxRecordedSpeed = Math.Max(this.MaxRecordedSpeed, kmph);
            this.MinRecordedSpeed = this.MinRecordedSpeed == 0 ? kmph : Math.Min(this.MinRecordedSpeed, kmph); // HACK (Cameron): Massive hack.
        }

        protected override object GetState()
        {
            return new CarState
            {
                Registration = this.Registration.Number,
                MaxRecordedSpeed = this.MaxRecordedSpeed,
                MinRecordedSpeed = this.MinRecordedSpeed,
            };
        }

        protected override void SetState(object memento)
        {
            var carState = (CarState)memento;
            this.Registration = new Registration(carState.Registration);
            this.MaxRecordedSpeed = carState.MaxRecordedSpeed;
            this.MinRecordedSpeed= carState.MinRecordedSpeed;
        }

        private class CarState
        {
            public string Registration { get; set; }

            public int MaxRecordedSpeed { get; set; }

            public int MinRecordedSpeed { get; set; }
        }
    }
}
