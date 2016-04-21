// <copyright file="Bug0081.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Tests.Bug
{
    using System;
    using FluentAssertions;
    using Memory;
    using Persistence.Sdk;
    using Xunit;

    public class Bug0081
    {
        [Fact]
        public void ShouldThrowForEventStoreRepository()
        {
            // arrange
            var identityMap = new MemoryIdentityMap();
            var repository = new EventStoreRepository(identityMap, new MemoryEventStore(), new MemorySnapshotStore());
            var naturalKey = "key";

            // act
            identityMap.GetOrAdd(typeof(Subject), typeof(string), naturalKey);
            Action action = () => repository.Load<Subject>(naturalKey);

            // assert
            action.ShouldThrow<AggregateRootNotFoundException>();
        }

        [Fact]
        public void ShouldThrowForConventionalRepository()
        {
            // arrange
            var identityMap = new MemoryIdentityMap();
            var repository = new MemoryRepository<Subject>(identityMap);
            var naturalKey = "key";

            // act
            identityMap.GetOrAdd(typeof(Subject), typeof(string), naturalKey);
            Action action = () => repository.Load(naturalKey);

            // assert
            action.ShouldThrow<AggregateRootNotFoundException>();
        }

        private class Subject : AggregateRoot
        {
            [NaturalKey]
            public string NaturalKey { get; set; }
        }
    }
}
