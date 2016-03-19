// <copyright file="SqlServerSnapshotStoreTests.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Tests.Integration
{
    using System;
    using dddlib.Persistence.SqlServer;
    using dddlib.Persistence.Tests.Sdk;
    using FluentAssertions;
    using Persistence.Sdk;
    using Xunit;

    public class SqlServerSnapshotStoreTests : Integration.Database
    {
        public SqlServerSnapshotStoreTests(SqlServerFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public void TrySaveSnapshot()
        {
            // arrange
            var snapshotStore = new SqlServerSnapshotStore(this.ConnectionString);
            var streamId = Guid.NewGuid();
            var expectedSnapshot = new Snapshot
            {
                StreamRevision = 4,
                Memento = new Memento { Id = 2, Name = "example" },
            };

            // act
            snapshotStore.PutSnapshot(streamId, expectedSnapshot);
            var actualSnapshot = snapshotStore.GetSnapshot(streamId);

            // assert
            actualSnapshot.Should().NotBeNull();
            actualSnapshot.ShouldBeEquivalentTo(expectedSnapshot);
        }

        [Fact]
        public void TryUpdateSnapshotRevision()
        {
            // arrange
            var snapshotStore = new SqlServerSnapshotStore(this.ConnectionString);
            var streamId = Guid.NewGuid();
            var firstSnapshot = new Snapshot
            {
                StreamRevision = 4,
                Memento = new Memento { Id = 2, Name = "first" },
            };

            var secondSnapshot = new Snapshot
            {
                StreamRevision = 8,
                Memento = new Memento { Id = 2, Name = "second" },
            };

            // act
            snapshotStore.PutSnapshot(streamId, firstSnapshot);
            snapshotStore.PutSnapshot(streamId, secondSnapshot);
            var actualSnapshot = snapshotStore.GetSnapshot(streamId);

            // assert
            actualSnapshot.Should().NotBeNull();
            actualSnapshot.ShouldBeEquivalentTo(secondSnapshot);
        }

        private class Memento
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }
    }
}
