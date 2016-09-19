// <copyright file="Bug0129.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Bug
{
    using System.Collections.Generic;
    using FluentAssertions;
    using Xunit;

    public class Bug0129
    {
        [Fact]
        public void ShouldNotThrow()
        {
            new Subject();
        }

        [Fact]
        public void ShouldBeEqual()
        {
            var a = new Subject { Elements = new[] { "hello" } };
            var b = new Subject { Elements = new List<string> { "hello" } };

            a.Should().Be(b);
        }

        public class Subject : ValueObject<Subject>
        {
            public IEnumerable<string> Elements { get; set; }
        }
    }
}
