// <copyright file = "Bug0043.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Tests.Bug
{
    using System;
    using dddlib.Persistence.Memory;
    using Xunit;

    // LINK (Cameron): https://github.com/dddlib/dddlib/issues/43
    public class Bug0043
    {
        [Fact]
        public void ShouldThrow()
        {
            var repository = new MemoryRepository<Car>();
            var registration = new Registraion { Number = "abc" };
            var car = new Car(registration);

            repository.Save(car);

            // NOTE (Cameron): This should throw because we're trying to load an aggregate with a natural key of type string using a value object.
            Assert.Throws<ArgumentException>(() => repository.Load(registration));
        }

        public class Car : AggregateRoot
        {
            public Car(Registraion registration)
            {
                Guard.Against.Null(() => registration);

                this.RegistrationNumber = registration.Number;
            }

            internal Car()
            {
            }

            [NaturalKey]
            public string RegistrationNumber { get; private set; }

            protected override object GetState()
            {
                return this.RegistrationNumber;
            }
        }

        public class Registraion : ValueObject<Registraion>
        {
            public string Number { get; set; }
        }
    }
}
