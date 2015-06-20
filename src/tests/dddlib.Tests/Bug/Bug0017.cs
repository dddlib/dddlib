// <copyright file="Bug0017.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Bug
{
    using dddlib.Runtime;
    using Xunit;

    public class Bug0017
    {
        [Fact]
        public void ShouldThrow()
        {
            var exception = Assert.Throws<RuntimeException>(() => new Thing());
            Assert.Null(exception.InnerException);
        }

        public class Thing : Entity
        {
            [NaturalKey]
            public string NaturalKey { get; set; }

            [NaturalKey]
            public string AnotherNaturalKey { get; set; }
        }
    }
}
