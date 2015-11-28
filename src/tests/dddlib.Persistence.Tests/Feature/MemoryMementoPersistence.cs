// <copyright file="MemoryMementoPersistence.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Tests.Feature
{
    using dddlib.Configuration;
    using dddlib.Persistence.Memory;
    using dddlib.Persistence.Tests.Sdk;
    using FluentAssertions;
    using Xbehave;

    // As someone who uses dddlib
    // In order save state
    // I need to be able to persist an aggregate root (in memory)
    public abstract class MemoryMementoPersistence : Feature
    {
        /*
            AggregateRoot Persistence (Guid)
            --------------------------------
            with natural key selector (undefined) AND with uninitialized factory (undefined)
            with natural key selector (defined - doesn't matter how) AND with uninitialized factory (undefined)
            with natural key selector (undefined) AND with uninitialized factory (defined in bootstrapper only)
            with natural key selector (defined - doesn't matter how) AND with uninitialized factory (defined in bootstrapper only)

            AggregateRoot Persistence (special case: string)
            ------------------------------------------------
            ALL FOLLOWING TEST: with natural key selector (defined - doesn't matter how) AND with uninitialized factory (defined in bootstrapper only)
            with natural key equality comparer (undefined)
            with natural key equality comparer (string only, defined in bootstrapper only)

            AggregateRoot Persistence (special case: composite key value object: strings)
            -----------------------------------------------------------------------------
            ALL FOLLOWING TEST: with natural key selector (defined - doesn't matter how) AND with uninitialized factory (defined in bootstrapper only)
            with natural key serializer (undefined)
            with natural key serializer (defined in bootstrapper)
            AND MORE?
        */

        public class DefaultMemoryPersistence : MemoryMementoPersistence
        {
            [Scenario]
            public void Scenario(IRepository<Subject> repository, Subject instance, Subject otherInstance, string naturalKey)
            {
                "Given a repository"
                    .f(() => repository = new MemoryRepository<Subject>());

                "And a natural key value"
                    .f(() => naturalKey = "key");

                "And an instance of an entity with that natural key"
                    .f(() => instance = new Subject(naturalKey));

                "When that instance is saved to the repository"
                    .f(() => repository.Save(instance));

                "And an other instance is loaded from the repository"
                    .f(() => otherInstance = repository.Load(instance.NaturalKey));

                "Then that instance should be the other instance"
                    .f(() => instance.Should().Be(otherInstance));
            }

            public class Subject : AggregateRoot
            {
                public Subject(string naturalKey)
                {
                    this.Apply(new NewSubject { NaturalKey = naturalKey });
                }

                internal Subject()
                {
                }

                public string NaturalKey { get; private set; }

                protected override object GetState()
                {
                    return this.NaturalKey;
                }

                protected override void SetState(object memento)
                {
                    this.NaturalKey = memento.ToString();
                }

                private void Handle(NewSubject @event)
                {
                    this.NaturalKey = @event.NaturalKey;
                }
            }

            public class NewSubject
            {
                public string NaturalKey { get; set; }
            }

            private class BootStrapper : IBootstrap<Subject>
            {
                public void Bootstrap(IConfiguration configure)
                {
                    configure.AggregateRoot<Subject>()
                        .ToUseNaturalKey(subject => subject.NaturalKey)
                        .ToReconstituteUsing(() => new Subject());
                }
            }
        }
    }
}
