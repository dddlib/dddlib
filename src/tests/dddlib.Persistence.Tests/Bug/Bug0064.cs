// <copyright file="Bug0064.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Tests.Bug
{
    using System;
    using dddlib.Persistence.Memory;
    using dddlib.Runtime;
    using Xunit;

    public class Bug0064
    {
        [Fact]
        public void ShouldThrowArgumentException()
        {
            var thing = new Thing();
            var repository = new MemoryRepository<Thing>();

            var exception = Assert.Throws<ArgumentException>(() => repository.Save(thing));

            Assert.Null(exception.InnerException);
            Assert.Contains("aggregateRoot.SomeKey", exception.Message);
        }

        [Fact]
        public void ShouldThrowPersistenceException()
        {
            var thing = new Thing
            {
                SomeKey = new InvalidKey { Value = "irrelevant" },
            };

            var repository = new MemoryRepository<Thing>();

            var exception = Assert.Throws<PersistenceException>(() => repository.Save(thing));

            Assert.NotNull(exception.InnerException);
            Assert.IsType<RuntimeException>(exception.InnerException);
        }

        public class Thing : AggregateRoot
        {
            [NaturalKey]
            public InvalidKey SomeKey { get; set; }
        }

        public class InvalidKey
        {
            public string Value { get; set; }
        }
    }
}
