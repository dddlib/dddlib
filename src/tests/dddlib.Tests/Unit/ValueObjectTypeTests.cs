// <copyright file="ValueObjectTypeTests.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Unit
{
    using System;
    using System.Collections.Generic;
    using dddlib.Runtime;
    using dddlib.Sdk;
    using dddlib.Sdk.Configuration.Model;
    using FluentAssertions;
    using Xunit;

    public class ValueObjectTypeTests
    {
        // happy path
        [Fact(Skip = "Needs re-writing")]
        public void ValueObjectTypeTest()
        {
            // arrange
            var type = typeof(Subject);
            var equalityComparer = new SubjectEqualityComparer();

            // act (and assert)
            ////var runtimeType = new ValueObjectType(type, equalityComparer, new MapperCollection());
        }

        [Fact(Skip = "Needs re-writing")]
        public void InavlidValueObjectTypeTest()
        {
            // arrange
            var type = typeof(object);
            var equalityComparer = new SubjectEqualityComparer();

            // act
            ////Action action = () => new ValueObjectType(type, equalityComparer, new MapperCollection());

            // assert
            ////action.ShouldThrow<RuntimeException>();
        }

        [Fact(Skip = "Needs re-writing")]
        public void InvalidValueObjectTypeEqualityComparerTest()
        {
            // arrange
            var type = typeof(Subject);
            var equalityComparer = new object();

            // act
            ////Action action = () => new ValueObjectType(type, equalityComparer, new MapperCollection());

            // assert
            ////action.ShouldThrow<RuntimeException>();
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
