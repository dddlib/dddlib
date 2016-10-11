// <copyright file="Bug0128.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Bug
{
    using System;
    using System.Collections.Generic;
    using FluentAssertions;
    using Runtime;
    using Xunit;

    public class Bug0128
    {
        [Fact]
        public void ShouldThrow()
        {
            Action action = () => new Subject("test");

            action.ShouldThrow<RuntimeException>();
        }

        [Fact]
        public void ShouldNotThrow()
        {
            Action action = () => new OtherSubject("test");

            action.ShouldNotThrow();
        }

        [Fact]
        public void AreEqual()
        {
            var a = new OtherSubject("test");
            var b = new OtherSubject("TEST");

            a.Should().Be(b);
        }

        public sealed class Subject : ValueObject<Subject>
        {
            private readonly string value;

            public Subject(string value)
            {
                this.value = value;
            }
        }

        public sealed class OtherSubject : ValueObject<OtherSubject>
        {
            private readonly string value;

            public OtherSubject(string value)
            {
                this.value = value;
            }

            internal class EqualityComparer : IEqualityComparer<OtherSubject>
            {
                public bool Equals(OtherSubject x, OtherSubject y)
                {
                    return string.Equals(x.value, y.value, StringComparison.OrdinalIgnoreCase);
                }

                public int GetHashCode(OtherSubject obj)
                {
                    return StringComparer.OrdinalIgnoreCase.GetHashCode(obj.value);
                }
            }
        }
    }
}
