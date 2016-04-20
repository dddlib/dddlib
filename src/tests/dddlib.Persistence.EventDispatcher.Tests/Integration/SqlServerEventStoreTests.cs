// <copyright file="SqlServerEventStoreTests.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.EventDispatcher.Tests.Integration
{
    using System;
    using dddlib.Persistence.SqlServer;
    using dddlib.Tests.Sdk;
    using Xunit;
    using SqlServerEventDispatcherEventStore = dddlib.Persistence.EventDispatcher.SqlServer.SqlServerEventStore;

    public class SqlServerEventStoreTests : Integration.Database
    {
        public SqlServerEventStoreTests(SqlServerFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public void Test()
        {
            // arrange
            var eventStore = new SqlServerEventStore(this.ConnectionString);
            var eventDispatcherEventStore = new SqlServerEventDispatcherEventStore(this.ConnectionString);
            var streamId = Guid.NewGuid();
            var events = new[]
            {
                new Event { Id = 1, Value = "One" },
            };

            string commitState;

            // act
            eventStore.CommitStream(streamId, events, Guid.NewGuid(), null, out commitState);
            var batch = eventDispatcherEventStore.GetNextUndispatchedEventsBatch(null, 50);
            var sameBatch = eventDispatcherEventStore.GetNextUndispatchedEventsBatch("same", 50);

            // assert
        }

        private class Event
        {
            public int Id { get; set; }

            public string Value { get; set; }
        }
    }
}