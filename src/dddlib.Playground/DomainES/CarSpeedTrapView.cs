namespace dddlib.Playground.DomainES
{
    using System;
    using System.Collections.Generic;

    public class CarSpeedTrapView
    {
        private readonly Dictionary<string, CarDto> store;

        public CarSpeedTrapView(Dictionary<string, CarDto> store)
        {
            Guard.Against.Null(() => store);

            this.store = store;
        }

        public void Handle(CarEnteredIntoSystem @event)
        {
            this.store.Add(
                @event.RegistrationNumber,
                new CarDto
                {
                    Registration = @event.RegistrationNumber,
                });

        }

        public void Handle(CarPassedThroughSpeedTrap @event)
        {
            var car = this.store[@event.RegistrationNumber];
            car.MaxRecordedSpeed = Math.Max(car.MaxRecordedSpeed, @event.Speed);
            car.MinRecordedSpeed = car.MinRecordedSpeed == 0 ? @event.Speed : Math.Min(car.MinRecordedSpeed, @event.Speed); // HACK (Cameron): Massive hack.
        }
    }
}
