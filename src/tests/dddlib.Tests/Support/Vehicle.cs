// <copyright file="Vehicle.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Support
{
    public abstract class Vehicle : AggregateRoot
    {
        public Vehicle(string registration)
        {
            Guard.Against.Null(() => registration);

            if (!registration.StartsWith("J"))
            {
                throw new BusinessException("Cannot create a vehicle registered in Jersey (CI) without a J-plate.");
            }

            this.ApplyChange(
                new VehicleRegistered
                {
                    Registration = registration,
                });
        }

        [NaturalKey]
        public string Registration { get; protected set; }

        public void Scrap()
        {
            this.ApplyChange(
                new VehicleScrapped
                {
                    Registration = this.Registration,
                });
        }

        private void Apply(VehicleRegistered carRegistered)
        {
            this.Registration = carRegistered.Registration;
        }
    }
}
