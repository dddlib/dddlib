// <copyright file="MemoryEventPersistence.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Tests.Feature
{
    using System;
    using dddlib.Configuration;
    using dddlib.Persistence;
    using dddlib.Persistence.Memory;
    using dddlib.Persistence.Tests.Sdk;
    using dddlib.Runtime;
    using FluentAssertions;
    using Xbehave;

    // As someone who uses dddlib [with event sourcing]
    // In order save state
    // I need to be able to persist an aggregate root (in memory)
    public abstract class MemoryEventPersistence : Feature
    {
        private IEventStoreRepository repository;

        [Background]
        public override void Background()
        {
            base.Background();

            "Given an event store repository"
                .f(() => this.repository = new EventStoreRepository(new MemoryIdentityMap(), new MemoryEventStore()));
        }

        public class UndefinedNaturalKey : MemoryEventPersistence
        {
            [Scenario]
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

        public class UndefinedUnititializedFactory : MemoryEventPersistence
        {
            [Scenario]
            public void Scenario(Subject instance, Action action)
            {
                "Given an instance of an aggregate root with no defined uninitialized factory"
                    .f(() => instance = new Subject());

                "When that instance is saved to the repository"
                    .f(() => action = () => this.repository.Save(instance));

                "Then a runtime exception is thrown"
                    .f(() => action.ShouldThrow<RuntimeException>());
            }

            public class Subject : AggregateRoot
            {
                [NaturalKey]
                public string Id { get; set; }
            }
        }

        public class NullNaturalKey : MemoryEventPersistence
        {
            [Scenario]
            public void Scenario(Subject instance, Action action)
            {
                "Given an instance of an aggregate root with a null natural key"
                    .f(() => instance = new Subject());

                "When that instance is saved to the repository"
                    .f(() => action = () => this.repository.Save(instance));

                "Then a persistence exception is thrown"
                    .f(() => action.ShouldThrow<ArgumentException>());
            }

            public class Subject : AggregateRoot
            {
                [NaturalKey]
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

        /*
         * in all - validate with memento comparison
         * 
         *  a. cannot save with undefined natural key
         *  b. cannot save with undefined reconstitution method
         *  
         *  1. can save and get
         *  2. can save and save and get
         *  3. can save and get and save and get
         *  4. can save and snapshot and get (with snapshot)
         *  5. can save and snapshot and save and get (with snapshot)
         *  6. can save and snapshot and get (without snapshot)
         *  7. can save and snapshot and save and get (without snapshot)
         *  
         *  i. memento validation test (SDK)
         */
    }
}
