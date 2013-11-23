// <copyright file="AggregateRootTests.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests
{
    using dddlib.Tests.Support;
    using FluentAssertions;

    public class AggregateRootTests
    {
        public void Do()
        {
            new Car("123");

            var registration = "J123";
            var car = new Car(registration);
            car.Registration.Should().Be(registration);
        }
    }
}
