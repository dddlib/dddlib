// <copyright file="MemoryEventDispatcher.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.EventDispatcher.Tests.Feature
{
    using System;
    using Configuration;
    using dddlib.Tests.Sdk;
    using FluentAssertions;
    using Persistence.Memory;
    using Persistence.Sdk;
    using Runtime;
    using Xbehave;

    public abstract class MemoryEventDispatcher : Feature
    {
        private IIdentityMap identityMap;
        private IEventStore eventStore;
        private ISnapshotStore snapshotStore;
        private IEventStoreRepository repository;

        [Background]
        public override void Background()
        {
            base.Background();

            "Given an identity map"
                .f(() => this.identityMap = new MemoryIdentityMap());

            "And an event store"
                .f(() => this.eventStore = new MemoryEventStore());

            "And a snapshot store"
                .f(() => this.snapshotStore = new MemorySnapshotStore());

            "And an event store repository"
                .f(() => this.repository = new EventStoreRepository(this.identityMap, this.eventStore, this.snapshotStore));
        }

        public class UndefinedNaturalKey : MemoryEventDispatcher
        {
            [Scenario(Skip = "Nonsense.")]
            public void Scenario(Subject instance, Action action)
            {
                "Given an instance of an aggregate root with no defined natural key"
                    .f(() => instance = new Subject());

                "When that instance is saved to the repository"
                    .f(() => action = () => this.repository.Save(instance));

                "Then a runtime exception is thrown"
                    .f(() => action.ShouldThrow<RuntimeException>());
            }

            public class Subject : AggregateRoot
            {
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
