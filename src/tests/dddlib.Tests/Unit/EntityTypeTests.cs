// <copyright file="EntityTypeTests.cs" company="dddlib contributors">
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

    public class EntityTypeTests
    {
        // happy path
        [Fact]
        public void EntityTypeTest()
        {
            // arrange
            var type = typeof(Subject);
            var naturalKeySelector = new NaturalKeySelector(type, "NaturalKey");
            var equalityComparer = StringComparer.OrdinalIgnoreCase;

            // act (and assert)
            var runtimeType = new EntityType(type, naturalKeySelector, equalityComparer, new MappingCollection());
        }

        [Fact]
        public void InavlidEntityTypeTest()
        {
            // arrange
            var type = typeof(Subject);
            var naturalKeySelector = new NaturalKeySelector(typeof(Other), "NaturalKey");
            var equalityComparer = StringComparer.OrdinalIgnoreCase;

            // act
            Action action = () => new EntityType(type, naturalKeySelector, equalityComparer, new MappingCollection());

            // assert
            action.ShouldThrow<RuntimeException>();
        }

        [Fact]
        public void InvalidENaturalKeySelectorTest()
        {
            // arrange
            var type = typeof(Subject);
            var naturalKeySelector = new NaturalKeySelector(typeof(Other), "NaturalKey");
            var equalityComparer = StringComparer.OrdinalIgnoreCase;

            // act
            Action action = () => new EntityType(type, naturalKeySelector, equalityComparer, new MappingCollection());

            // assert
            action.ShouldThrow<RuntimeException>();
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
