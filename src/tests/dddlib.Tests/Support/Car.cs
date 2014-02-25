// <copyright file="Car.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Support
{
    using System.Collections.Generic;

    public class Car : Vehicle
    {
        public Car(string registration)
            : base(registration)
        {
        }

        protected internal Car()
        {
        }

        [NaturalKey(EqualityComparer = typeof(Comp))]
        public override string Registration
        {
            get { return base.Registration; }
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

        private class Comp : IEqualityComparer<string>
        {
            public bool Equals(string x, string y)
            {
                return true;
            }

            public int GetHashCode(string obj)
            {
                return 1;
            }
        }
    }
}
