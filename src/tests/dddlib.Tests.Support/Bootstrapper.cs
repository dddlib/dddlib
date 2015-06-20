// <copyright file="Bootstrapper.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Support
{
    using System;
    using System.Collections.Generic;
    using dddlib.Configuration;
    using dddlib.Runtime;

    internal class Bootstrapper : IBootstrapper
    {
        public void Bootstrap(IConfiguration configure)
        {
            configure.Entity<Vehicle>()
                .ToUseNaturalKey(vehicle => vehicle.Registration);

            configure.ValueObject<Registration>()
                .ToUseEqualityComparer(new RegistrationEqualityComparer())
                .ToUseValueObjectSerializer(new RegistrationSerializer());
        }

        private class RegistrationEqualityComparer : IEqualityComparer<Registration>
        {
            public bool Equals(Registration x, Registration y)
            {
                return string.Equals(x.Number, y.Number, StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode(Registration obj)
            {
                return obj.Number.GetHashCode();
            }
        }

        private class RegistrationSerializer : IValueObjectSerializer
        {
            public string Serialize(object valueObject)
            {
                var registration = (Registration)valueObject;
                return registration.Number;
            }

            public object Deserialize(string serializedValueObject)
            {
                return new Registration(serializedValueObject);
            }
        }
    }
}