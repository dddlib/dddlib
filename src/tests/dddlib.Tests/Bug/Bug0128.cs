// <copyright file="Bug0128.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Bug
{
    using System;
    using System.Collections.Generic;
    using FluentAssertions;
    using Xunit;

    public class Bug0128
    {
        [Fact]
        public void RespectLifecycle()
        {
            // arrange
            var a = new Subject("test");
            var b = new Subject("TEST");

            // assert
            a.Should().NotBe(b);
            (a == b).Should().BeFalse();
        }

        public sealed class Subject : ValueObject<Subject>
        {
            private readonly string value;

            public Subject(string value)
            {
                this.value = value;
            }
        }
    }
}
