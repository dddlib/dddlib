// <copyright file="Car.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Support
{
    public class Car : Vehicle
    {
        public Car(string registration)
            : base(registration)
        {
        }

        protected internal Car()
        {
        }

        protected override object GetState()
        {
            return new CarMemento
            {
                Registration = this.Registration,
            };
        }

        protected override void SetState(object memento)
        {
            var car = memento as CarMemento;
            if (car == null)
            {
                return;
            }

            this.Registration = car.Registration;
        }

        private class CarMemento
        {
            public string Registration { get; set; }
        }
    }
}
