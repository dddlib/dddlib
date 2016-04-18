// <copyright file="SqlServerEventDispatcher.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.EventDispatcher.Tests.Feature
{
    using System.Threading;
    using Configuration;
    using dddlib.Tests.Sdk;
    using FluentAssertions;
    using Persistence.Sdk;
    using Persistence.SqlServer;
    using Xbehave;
    using Xunit;

    // As someone who uses dddlib [with event sourcing]
    // In order to handle changes in the projections
    // I need to be able to dispatch events (from SQL Server)
    [Collection("SQL Server Collection")]
    public abstract class SqlServerEventDispatcher : SqlServerFeature
    {
        private IIdentityMap identityMap;
        private IEventStore eventStore;
        private ISnapshotStore snapshotStore;
        private IEventStoreRepository repository;

        public SqlServerEventDispatcher(SqlServerFixture fixture)
            : base(fixture)
        {
        }

        [Background]
        public override void Background()
        {
            base.Background();

            "Given an identity map"
                .f(() => this.identityMap = new SqlServerIdentityMap(this.ConnectionString));

            "And an event store"
                .f(() => this.eventStore = new SqlServerEventStore(this.ConnectionString));

            "And a snapshot store"
                .f(() => this.snapshotStore = new SqlServerSnapshotStore(this.ConnectionString));

            "And an event store repository"
                .f(() => this.repository = new EventStoreRepository(this.identityMap, this.eventStore, this.snapshotStore));
        }

        public class CanDispatch : SqlServerEventDispatcher
        {
            public CanDispatch(SqlServerFixture fixture)
                : base(fixture)
            {
            }

            [Scenario]
            public void Scenario(
                Subject instance,
                dddlib.Persistence.EventDispatcher.Sdk.EventDispatcher eventDispatcher,
                NewSubject newSubject,
                AutoResetEvent notify)
            {
                "Given a SQL Server event dispatcher"
                    .f(c =>
                    {
                        notify = new AutoResetEvent(false);
                        eventDispatcher = new SqlServer.SqlServerEventDispatcher(
                            this.ConnectionString,
                            (sequenceNumber, @event) =>
                            {
                                newSubject = @event as NewSubject;
                                notify.Set();
                            }).Using(c);
                    });

                "And an instance of an aggregate root"
                    .f(() => instance = new Subject("key"));

                "When that instance is saved to the repository"
                    .f(() => this.repository.Save(instance));

                "And a short period of time elapses"
                    .f(() => notify.WaitOne(10 * 1000) /* up to 10 secs */);

                "Then the event is dispatched"
                    .f(() =>
                    {
                        newSubject.Should().NotBeNull();
                        newSubject.Id.Should().Be(instance.Id);
                    });
            }

            public class Subject : AggregateRoot
            {
                public Subject(string id)
                {
                    this.Apply(new NewSubject { Id = id });
                }

                internal Subject()
                {
                }

                [NaturalKey]
                public string Id { get; private set; }

                protected override object GetState()
                {
                    return this.Id;
                }

                protected override void SetState(object memento)
                {
                    this.Id = memento.ToString();
                }

                private void Handle(NewSubject @event)
                {
                    this.Id = @event.Id;
                }
            }

            public class NewSubject
            {
                public string Id { get; set; }
            }

            private class BootStrapper : IBootstrap<Subject>
            {
                public void Bootstrap(IConfiguration configure)
                {
                    configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
                }
            }
        }
    }
}
