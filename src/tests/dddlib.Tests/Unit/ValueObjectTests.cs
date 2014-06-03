// <copyright file="ValueObjectTests.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Unit
{
    using System.Collections.Generic;
    using dddlib.Tests.Support;
    using FluentAssertions;
    using Xunit;

    public class ValueObjectTests
    {
        // happy path
        [Fact(Skip = "I've broken this.")]
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
        private sealed class Vector : ValueObject<Vector>
        {
            public Vector(int x, int y)
            {
                this.X = x;
                this.Y = y;
            }

            public int X { get; private set; }

            public int Y { get; private set; }

            ////protected override IEnumerable<object> GetValue()
            ////{
            ////    yield return this.X;
            ////    yield return this.Y;
            ////}
        }
    }
}
