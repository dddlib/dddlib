// <copyright file="EntityTests.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests
{
    using dddlib.Tests.Support;
    using Xunit;

    public class EntityTests
    {
        // test equality
        // test natural key value resolution
        [Fact(Skip = "Doesn't work.")]
        public void X()
        {
            var car = new Car("JBC");
            var car2 = new Car("JBC1");

            var areEqual = car == car2;
        }
    }
}
