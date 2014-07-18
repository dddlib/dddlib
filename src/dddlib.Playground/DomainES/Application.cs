namespace dddlib.Playground.DomainES
{
    using System;
    using System.Collections.Generic;
    using dddlib.Persistence;
    using dddlib.Persistence.Memory;

    public class Application
    {
        public void Demonstrate()
        {
            var eventStore = new MemoryEventStore();
            var repository = new EventStoreRepository(new MemoryIdentityMap(), eventStore);

            // enter a car into the system (so it can go through the speed trap)
            var registration = new Registration("BD02 XYZ");
            var car = new Car(registration);
            repository.Save(car);

            // speed trap #1
            var sameCar = repository.Load<Car>(registration);
            sameCar.PassedThroughSpeedTrapAt(67);
            repository.Save(sameCar);

            // speed trap #2
            var stillSameCar = repository.Load<Car>(registration);
            stillSameCar.PassedThroughSpeedTrapAt(167);
            repository.Save(stillSameCar);

            // what happens here?
            //repository.Save(sameCar);

            // NOTE (Cameron): This is nonsense.
            var viewState = new Dictionary<string, CarDto>();
            var view = new CarSpeedTrapView(viewState);
            eventStore.ReplayEventsTo(view);

            Console.WriteLine("Car: {0}", viewState[registration.Number].Registration);
            Console.WriteLine("Max speed: {0}", viewState[registration.Number].MaxRecordedSpeed);
            Console.WriteLine("Min speed: {0}", viewState[registration.Number].MinRecordedSpeed);
        }
    }
}
