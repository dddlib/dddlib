// <copyright file="Vehicle.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Support
{
    public class Vehicle : Entity
    {
        public Vehicle(Registration registration)
        {
            Guard.Against.Null(() => registration);
        }

        [NaturalKey]
        public Registration Registration { get; private set; }
    }
}
