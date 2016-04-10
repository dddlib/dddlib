// <copyright file="SqlServerEventDispatcher.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.EventDispatcher.Tests.Feature
{
    using System;
    using Configuration;
    using dddlib.Tests.Sdk;
    using FluentAssertions;
    using Persistence.Sdk;
    using Persistence.SqlServer;
    using Runtime;
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
            public void Scenario(Subject instance, Action action, Action<long, object> eventDispatcherDelegate)
            {
                "Given an event dispatcher delegate"
                    .f(() => eventDispatcherDelegate = (sequenceNumber, @event) => { });

                "And a SQL Server event dispatcher for that delegate"
                    .f(() => new SqlServer.SqlServerEventDispatcher(this.ConnectionString, eventDispatcherDelegate));

                "And an instance of an aggregate root"
                    .f(() => instance = new Subject("key"));

                "When that instance is saved to the repository"
                    .f(() => action = () => this.repository.Save(instance));

                "Then the event is dispatched"
                    .f(() => System.Threading.Tasks.Task.Delay(1000));
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
