// <copyright file="SqlServerEventPersistence.cs" company="dddlib contributors">
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
    // I need to be able to persist an aggregate root (in SQL Server)
    public abstract class SqlServerEventPersistence : EventPersistenceFeature
    {
        public SqlServerEventPersistence()
            : base(new EventStoreRepository(new MemoryIdentityMap(), new MemoryEventStore()))
        {
        }

        public class UndefinedNaturalKey : SqlServerEventPersistence
        {
            [Scenario]
            public void Scenario(Subject instance, Action action)
            {
                "Given an instance of an aggregate root with no defined natural key"
                    .f(() => instance = new Subject());

                "When that instance is saved to the repository"
                    .f(() => action = () => this.Repository.Save(instance));

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
                    // should be aggregate root
                    configure.AggregateRoot<Subject>()
                        .ToReconstituteUsing(() => new Subject());
                }
            }
        }
    }
}
