// <copyright file="Bug0128.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Bug
{
    using System;
    using FluentAssertions;
    using Runtime;
    using Xunit;

    public class Bug0128
    {
        [Fact]
        public void RespectLifecycle()
        {
            Action action = () => new Subject("test");

            action.ShouldThrow<RuntimeException>();
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
