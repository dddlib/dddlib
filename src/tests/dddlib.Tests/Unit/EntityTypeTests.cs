// <copyright file="EntityTypeTests.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Unit
{
    using System;
    using dddlib.Runtime;
    using dddlib.Sdk;
    using dddlib.Sdk.Configuration.Model;
    using FluentAssertions;
    using Xunit;

    public class EntityTypeTests
    {
        // happy path
        [Fact(Skip = "Needs re-writing")]
        public void EntityTypeTest()
        {
            // arrange
            var type = typeof(Subject);
            var naturalKeySelector = new NaturalKeySelector(type, "NaturalKey");
            var equalityComparer = StringComparer.OrdinalIgnoreCase;

            // act (and assert)
            ////var runtimeType = new EntityType(type, naturalKeySelector, equalityComparer, new MapperCollection());
        }

        [Fact(Skip = "Needs re-writing")]
        public void InavlidEntityTypeTest()
        {
            // arrange
            var type = typeof(Subject);
            var naturalKeySelector = new NaturalKeySelector(typeof(Other), "NaturalKey");
            var equalityComparer = StringComparer.OrdinalIgnoreCase;

            // act
            ////Action action = () => new EntityType(type, naturalKeySelector, equalityComparer, new MapperCollection());

            // assert
            ////action.ShouldThrow<RuntimeException>();
        }

        [Fact(Skip = "Needs re-writing")]
        public void InvalidENaturalKeySelectorTest()
        {
            // arrange
            var type = typeof(Subject);
            var naturalKeySelector = new NaturalKeySelector(typeof(Other), "NaturalKey");
            var equalityComparer = StringComparer.OrdinalIgnoreCase;

            // act
            ////Action action = () => new EntityType(type, naturalKeySelector, equalityComparer, new MapperCollection());

            // assert
            ////action.ShouldThrow<RuntimeException>();
        }

        private class Subject : Entity
        {
            public string NaturalKey { get; set; }
        }

        private class Other : Entity
        {
            public string NaturalKey { get; set; }
        }
    }
}
