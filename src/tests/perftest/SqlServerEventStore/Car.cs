// <copyright file="Car.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace perftest.SqlServerEventStore
{
    using dddlib;

    public class Car : AggregateRoot
    {
        protected internal Car()
        {
        }

        public Car(string registration)
        {
            this.Apply(new CarRegistered { Registration = registration });
        }

        [NaturalKey]
        public string Registration { get; set; }

        public void Drive(int distance)
        {
            this.Apply(new CarDriven { Registration = this.Registration, Distance = distance });
        }

        public void Scrap()
        {
            this.Apply(new CarScrapped { Registration = this.Registration });
        }

        private void Handle(CarRegistered @event)
        {
            this.Registration = @event.Registration;
        }

        private void Handle(CarScrapped @event)
        {
            this.EndLifecycle();
        }
    }
}
