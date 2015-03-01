namespace dddlib.Playground.Domain
{
    using System;

    public class Car
    {
        public Car(Registration registration)
        {
            Guard.Against.Null(() => registration);

            this.Registration = registration;
        }

        public Registration Registration { get; private set; }

        public int MaxRecordedSpeed { get; private set; }

        public int MinRecordedSpeed { get; private set; }

        public void PassedThroughSpeedTrapAt(int kmph)
        {
            this.MaxRecordedSpeed = Math.Max(this.MaxRecordedSpeed, kmph);
            this.MinRecordedSpeed = this.MinRecordedSpeed == 0 ? kmph : Math.Min(this.MinRecordedSpeed, kmph); // HACK (Cameron): Massive hack.
        }
    }
}
