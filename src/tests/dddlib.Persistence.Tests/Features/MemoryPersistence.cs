// <copyright file="MemoryPersistence.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Tests
{
    using dddlib.Configuration;
    using dddlib.Persistence.Memory;
    using dddlib.Tests.Sdk;
    using FluentAssertions;
    using Xbehave;

    // As someone who uses dddlib
    // In order save state
    // I need to be able to persist an aggregate root
    public abstract class MemoryPersistence : Feature
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

        public class DefinedInBootstrapper : MemoryPersistence
        {
            [Scenario]
            public void Scenario(MemoryRepository<Subject> repository, Subject instance, Subject otherInstance, string naturalKey)
            {
                "Given a repository"
                    .Given(() => repository = new MemoryRepository<Subject>());

                "And a natural key value"
                    .And(() => naturalKey = "key");

                "And an instance of an entity with that natural key"
                    .And(() => instance = new Subject(naturalKey));

                "When that instance is saved to the repository"
                    .When(() => repository.Save(instance));

                "And an other instance is loaded from the repository"
                    .And(() => otherInstance = repository.Load(instance.NaturalKey));

                "Then that instance should be the other instance"
                    .Then(() => instance.Should().Be(otherInstance));
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
                    // should be aggregate root
                    configure.AggregateRoot<Subject>()
                        .ToUseNaturalKey(subject => subject.NaturalKey)
                        .ToReconstituteUsing(() => new Subject());
                }
            }
        }
    }
}
