// <copyright file="EntityTests.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Unit
{
    using dddlib.Tests.Acceptnace.Support;
    using FluentAssertions;
    using Xunit;

    public class EntityTests
    {
        [Fact]
        public void X()
        {
            var car = new Car(new Registration("JBC"));
            var car2 = new Car(new Registration("JBC"));
            var car3 = new Car(new Registration("JBC1"));

            (car == car2).Should().BeTrue();
            (car == car3).Should().BeFalse();
        }
    }
}
