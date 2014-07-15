namespace dddlib.Playground.Domain
{
    using System;

    public class Application
    {
        public void Demonstrate()
        {
            var repository = new MemoryRepository();

            // enter a car into the system (so it can go through the speed trap)
            var registration = new Registration("BD02 XYZ");
            var car = new Car(registration);
            repository.Save(car);

            // speed trap #1
            var sameCar = repository.Load(registration.Number);
            sameCar.PassedThroughSpeedTrapAt(67);
            repository.Save(sameCar);

            // speed trap #2
            var stillSameCar = repository.Load(registration.Number);
            stillSameCar.PassedThroughSpeedTrapAt(167);
            repository.Save(stillSameCar);

            // what happens here?
            //repository.Save(sameCar);

            Console.WriteLine("Car: {0}", stillSameCar.Registration.Number);
            Console.WriteLine("Max speed: {0}", stillSameCar.MaxRecordedSpeed);
            Console.WriteLine("Min speed: {0}", stillSameCar.MinRecordedSpeed);
        }
    }
}
