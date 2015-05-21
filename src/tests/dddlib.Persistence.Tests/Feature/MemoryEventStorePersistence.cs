// <copyright file="MemoryEventStorePersistence.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Tests.Feature
{
    using System;
    using dddlib.Configuration;
    using dddlib.Persistence;
    using dddlib.Persistence.Memory;
    using dddlib.Runtime;
    using dddlib.Tests.Sdk;
    using FluentAssertions;
    using Xbehave;

    // As someone who uses dddlib [with event sourcing]
    // In order save state
    // I need to be able to persist an aggregate root
    public abstract class MemoryEventStorePersistence : Feature
    {
        public class UndefinedNaturalKey : MemoryEventStorePersistence
        {
            [Scenario]
            public void Scenario(EventStoreRepository repository, Subject instance, Action action)
            {
                "Given a repository"
                    .Given(() => repository = new EventStoreRepository(new MemoryIdentityMap(), new MemoryEventStore()));

                "And an instance of an aggregate root"
                    .And(() => instance = new Subject());

                "When that instance is saved to the repository"
                    .When(() => action = () => repository.Save(instance));

                "Then a runtime exception is thrown"
                    .Then(() => action.ShouldThrow<RuntimeException>());
            }

            public class Subject : AggregateRoot
            {
            }

            private class BootStrapper : IBootstrap<Subject>
            {
                public void Bootstrap(IConfiguration configure)
                {
                    // should be aggregate root
                    configure.AggregateRoot<Subject>()
                        .ToReconstituteUsing(() => new Subject());
                }
            }
        }
    }
}
