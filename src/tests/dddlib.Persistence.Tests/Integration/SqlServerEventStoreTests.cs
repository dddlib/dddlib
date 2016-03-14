// <copyright file="SqlServerEventStoreTests.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Tests.Integration
{
    using System;
    using System.Linq;
    using dddlib.Persistence.SqlServer;
    using dddlib.Persistence.Tests.Sdk;
    using FluentAssertions;
    using Xunit;

    public class SqlServerEventStoreTests : Integration.Database
    {
        public SqlServerEventStoreTests(SqlServerFixture fixture)
            : base(fixture)
        {
        }

        // add single event
        // add multiple events
        // add 2x events with concurrency exception (client side)
        // add 2x events with concurrency exception (server side)
        [Fact]
        public void TrySaveSingleEvent()
        {
            // arrange
            var eventStore = new SqlServerEventStore(this.ConnectionString);
            var streamId = Guid.NewGuid();
            var events = new[]
            {
                new Event { Id = 1, Value = "One" },
            };

            string commitState;
            string actualCommitState;

            // act
            eventStore.CommitStream(streamId, events, Guid.NewGuid(), null, out commitState);
            var actualEvents = eventStore.GetStream(streamId, 0, out actualCommitState);

            // assert
            actualEvents.Count().Should().Be(1);
            actualEvents.Single().Should().BeOfType<Event>();
            actualEvents.Cast<Event>().Single().ShouldBeEquivalentTo(events[0]);
            actualCommitState.Should().Be(commitState);
        }

        [Fact]
        public void TrySaveMultipleEvents()
        {
            // arrange
            var eventStore = new SqlServerEventStore(this.ConnectionString);
            var streamId = Guid.NewGuid();
            var events = new[]
            {
                new Event { Id = 1, Value = "One" },
                new Event { Id = 2, Value = "Two" },
            };

            string commitState;
            string actualCommitState;

            // act
            eventStore.CommitStream(streamId, events, Guid.NewGuid(), null, out commitState);
            var actualEvents = eventStore.GetStream(streamId, 0, out actualCommitState);

            // assert
            actualEvents.Count().Should().Be(2);
            actualEvents.First().Should().BeOfType<Event>();
            actualEvents.Last().Should().BeOfType<Event>();
            actualEvents.Cast<Event>().First().ShouldBeEquivalentTo(events[0]);
            actualEvents.Cast<Event>().Last().ShouldBeEquivalentTo(events[1]);
            actualCommitState.Should().Be(commitState);
        }

        [Fact]
        public void TrySaveEventsInMultipleCommits()
        {
            // arrange
            var eventStore = new SqlServerEventStore(this.ConnectionString);
            var streamId = Guid.NewGuid();
            var events1 = new[] { new Event { Id = 1, Value = "One" } };
            var events2 = new[] { new Event { Id = 2, Value = "Two" } };

            string firstCommitState;
            string secondCommitState;
            string actualCommitState;

            // act
            eventStore.CommitStream(streamId, events1, Guid.NewGuid(), null, out firstCommitState);
            eventStore.CommitStream(streamId, events2, Guid.NewGuid(), firstCommitState, out secondCommitState);
            var actualEvents = eventStore.GetStream(streamId, 0, out actualCommitState);

            // assert
            actualEvents.Count().Should().Be(2);
            actualEvents.First().Should().BeOfType<Event>();
            actualEvents.Last().Should().BeOfType<Event>();
            actualEvents.Cast<Event>().First().ShouldBeEquivalentTo(events1[0]);
            actualEvents.Cast<Event>().Last().ShouldBeEquivalentTo(events2[0]);
            actualCommitState.Should().Be(secondCommitState);
        }

        [Fact]
        public void TrySaveEventsForMultipleStreams()
        {
            // arrange
            var eventStore = new SqlServerEventStore(this.ConnectionString);
            var stream1Id = Guid.NewGuid();
            var stream2Id = Guid.NewGuid();
            var events1 = new[] { new Event { Id = 1, Value = "One" } };
            var events2 = new[] { new Event { Id = 2, Value = "Two" } };

            string stream1CommitState;
            string stream2CommitState;
            string actualStream1CommitState;
            string actualStream2CommitState;

            // act
            eventStore.CommitStream(stream1Id, events1, Guid.NewGuid(), null, out stream1CommitState);
            eventStore.CommitStream(stream2Id, events2, Guid.NewGuid(), null, out stream2CommitState);
            var stream1Events = eventStore.GetStream(stream1Id, 0, out actualStream1CommitState);
            var stream2Events = eventStore.GetStream(stream2Id, 0, out actualStream2CommitState);

            // assert
            stream1Events.Count().Should().Be(1);
            stream1Events.Single().Should().BeOfType<Event>();
            stream1Events.Cast<Event>().Single().ShouldBeEquivalentTo(events1[0]);
            actualStream1CommitState.Should().Be(stream1CommitState);
            stream2Events.Count().Should().Be(1);
            stream2Events.Single().Should().BeOfType<Event>();
            stream2Events.Cast<Event>().Single().ShouldBeEquivalentTo(events2[0]);
            actualStream2CommitState.Should().Be(stream2CommitState);
        }

        private class Event
        {
            public int Id { get; set; }

            public string Value { get; set; }
        }
    }
}
