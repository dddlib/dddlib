// <copyright file="SqlServerEventStoreTests.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.EventDispatcher.Tests.Integration
{
    using System;
    using dddlib.Persistence.SqlServer;
    using dddlib.Tests.Sdk;
    using FluentAssertions;
    using Xunit;
    using SqlServerEventDispatcherEventStore = dddlib.Persistence.EventDispatcher.SqlServer.SqlServerEventStore;

    public class SqlServerEventStoreTests : Integration.Database, IDisposable
    {
        public SqlServerEventStoreTests(SqlServerFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public void TryGetBatchFromEmptyEventStore()
        {
            // arrange
            var eventDispatcherEventStore = new SqlServerEventDispatcherEventStore(this.ConnectionString);

            // act
            var batch = eventDispatcherEventStore.GetNextUndispatchedEventsBatch(Guid.Empty, 50);

            // assert
            batch.Should().BeNull();
        }

        [Fact]
        public void TryGetBatchFromEventStoreWithSingleEvent()
        {
            // arrange
            var eventDispatcherEventStore = new SqlServerEventDispatcherEventStore(this.ConnectionString);
            var eventStore = new SqlServerEventStore(this.ConnectionString);
            var streamId = Guid.NewGuid();
            var events = new[]
            {
                new Event { Id = 1, Value = "One" },
            };

            string commitState;
            eventStore.CommitStream(streamId, events, Guid.NewGuid(), null, out commitState);

            // act
            var batch = eventDispatcherEventStore.GetNextUndispatchedEventsBatch(Guid.Empty, 50);

            // assert
            batch.Should().NotBeNull();
            batch.Events.Should().ContainSingle(@event => @event.Payload.As<Event>().Id == 1 && @event.Payload.As<Event>().Value == "One");
        }

        [Fact]
        public void TryGetBatchTwiceFromEventStoreWithSingleEvent()
        {
            // arrange
            var eventDispatcherEventStore = new SqlServerEventDispatcherEventStore(this.ConnectionString);
            var eventStore = new SqlServerEventStore(this.ConnectionString);
            var streamId = Guid.NewGuid();
            var events = new[]
            {
                new Event { Id = 2, Value = "Two" },
            };

            string commitState;
            eventStore.CommitStream(streamId, events, Guid.NewGuid(), null, out commitState);

            // act
            var firstBatch = eventDispatcherEventStore.GetNextUndispatchedEventsBatch(Guid.Empty, 50);
            var secondBatch = eventDispatcherEventStore.GetNextUndispatchedEventsBatch(Guid.Empty, 50);

            // assert
            firstBatch.Should().NotBeNull();
            firstBatch.Events.Should().ContainSingle(@event => @event.Payload.As<Event>().Id == 2 && @event.Payload.As<Event>().Value == "Two");
            secondBatch.Should().BeNull();
        }

        [Fact]
        public void TryGetBatchTwiceFromEventStoreWithSingleEventAndDifferentDispatchers()
        {
            // arrange
            var eventDispatcherEventStore = new SqlServerEventDispatcherEventStore(this.ConnectionString);
            var eventStore = new SqlServerEventStore(this.ConnectionString);
            var streamId = Guid.NewGuid();
            var events = new[]
            {
                new Event { Id = 3, Value = "Three" },
            };

            string commitState;
            eventStore.CommitStream(streamId, events, Guid.NewGuid(), null, out commitState);

            // act
            var firstBatch = eventDispatcherEventStore.GetNextUndispatchedEventsBatch(Guid.NewGuid(), 50);
            var secondBatch = eventDispatcherEventStore.GetNextUndispatchedEventsBatch(Guid.NewGuid(), 50);

            // assert
            firstBatch.Should().NotBeNull();
            firstBatch.Events.Should().ContainSingle(@event => @event.Payload.As<Event>().Id == 3 && @event.Payload.As<Event>().Value == "Three");
            secondBatch.Should().NotBeNull();
            secondBatch.Events.Should().ContainSingle(@event => @event.Payload.As<Event>().Id == 3 && @event.Payload.As<Event>().Value == "Three");
        }

        [Fact]
        public void TryGetMultipleBatchesFromEventStoreWithManyEvents()
        {
            // arrange
            var eventDispatcherEventStore = new SqlServerEventDispatcherEventStore(this.ConnectionString);
            var eventStore = new SqlServerEventStore(this.ConnectionString);
            var streamId = Guid.NewGuid();
            var events = new[]
            {
                new Event { Id = 4, Value = "Four" },
                new Event { Id = 5, Value = "Five" },
                new Event { Id = 6, Value = "Six" },
            };

            string commitState;
            eventStore.CommitStream(streamId, events, Guid.NewGuid(), null, out commitState);

            // act
            var firstBatch = eventDispatcherEventStore.GetNextUndispatchedEventsBatch(Guid.Empty, 2);
            var secondBatch = eventDispatcherEventStore.GetNextUndispatchedEventsBatch(Guid.Empty, 2);

            // assert
            firstBatch.Should().NotBeNull();
            firstBatch.Events.Should().Contain(@event => @event.Payload.As<Event>().Id == 4 && @event.Payload.As<Event>().Value == "Four");
            firstBatch.Events.Should().Contain(@event => @event.Payload.As<Event>().Id == 5 && @event.Payload.As<Event>().Value == "Five");
            secondBatch.Should().NotBeNull();
            secondBatch.Events.Should().ContainSingle(@event => @event.Payload.As<Event>().Id == 6 && @event.Payload.As<Event>().Value == "Six");
        }

        public void Dispose()
        {
            this.ExecuteScript(@"DELETE FROM [dbo].[Batches];
DELETE FROM [dbo].[DispatchedEvents];
DELETE FROM [dbo].[Events];
DELETE FROM [dbo].[Types];");
        }

        private class Event
        {
            public int Id { get; set; }

            public string Value { get; set; }
        }
    }
}