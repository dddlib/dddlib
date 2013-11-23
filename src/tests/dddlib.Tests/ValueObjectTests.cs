// <copyright file="ValueObjectTests.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests
{
    using dddlib.Tests.Support;
    using FluentAssertions;
    using Xunit;

    public class ValueObjectTests
    {
        // happy path
        [Fact]
        public void VectorEqualityTest()
        {
            var firstVector = new Vector(1, 2);
            var secondVector = new Vector(1, 2);

            firstVector.Should().Be(secondVector);
        }

        // sad path
        [Fact]
        public void VectorInequalityTest()
        {
            var firstVector = new Vector(1, 2);
            var secondVector = new Vector(3, 4);

            firstVector.Should().NotBe(secondVector);
        }

        // test complex equality
    }
}
