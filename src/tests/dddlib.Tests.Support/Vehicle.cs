// <copyright file="Vehicle.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Support
{
    using dddlib.Runtime;

    public class Vehicle : AggregateRoot
    {
        protected internal Vehicle()
        {
        }

        public Vehicle(Registration registration)
        {
            Guard.Against.Null(() => registration);

            this.Apply(new NewVehicle { RegistrationNumber = registration.Number });
        }

        [NaturalKey]
        public Registration Registration { get; private set; }

        private void Handle(NewVehicle @event)
        {
            if (this.Registration != null)
            {
                throw new RuntimeException("Event processed twice!");
            }

            this.Registration = new Registration(@event.RegistrationNumber);
        }
    }
}
