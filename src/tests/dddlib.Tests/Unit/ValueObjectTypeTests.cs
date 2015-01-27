// <copyright file="ValueObjectTypeTests.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Unit
{
    using System;
    using System.Collections.Generic;
    using dddlib.Runtime;
    using dddlib.Sdk;
    using FluentAssertions;
    using Xunit;

    public class ValueObjectTypeTests
    {
        // happy path
        [Fact]
        public void ValueObjectTypeTest()
        {
            // arrange
            var type = typeof(Subject);
            var equalityComparer = new SubjectEqualityComparer();

            // act (and assert)
            var runtimeType = new ValueObjectType(type, equalityComparer, new MappingCollection());
        }

        [Fact]
        public void InavlidValueObjectTypeTest()
        {
            // arrange
            var type = typeof(object);
            var equalityComparer = new SubjectEqualityComparer();

            // act
            Action action = () => new ValueObjectType(type, equalityComparer, new MappingCollection());

            // assert
            action.ShouldThrow<RuntimeException>();
        }

        [Fact]
        public void InvalidValueObjectTypeEqualityComparerTest()
        {
            // arrange
            var type = typeof(Subject);
            var equalityComparer = new object();

            // act
            Action action = () => new ValueObjectType(type, equalityComparer, new MappingCollection());

            // assert
            action.ShouldThrow<RuntimeException>();
        }

        private class Subject : ValueObject<Subject>
        {
        }

        private class SubjectEqualityComparer : IEqualityComparer<Subject>
        {
            public bool Equals(Subject x, Subject y)
            {
                throw new NotImplementedException();
            }

            public int GetHashCode(Subject obj)
            {
                throw new NotImplementedException();
            }
        }
    }
}
