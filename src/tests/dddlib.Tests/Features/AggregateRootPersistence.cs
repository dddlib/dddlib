// <copyright file="AggregateRootPersistence.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Features
{
    using dddlib.Configuration;
    using dddlib.Tests.Sdk;
    using FluentAssertions;
    using Xbehave;

    // As someone who uses dddlib
    // In order save state
    // I need to be able to persist an aggregate root
    public abstract class AggregateRootPersistence : Feature
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

        public class DefinedInBootstrapper : AggregateRootPersistence
        {
            [Scenario]
            public void Scenario(Subject instance1, Subject instance2, string naturalKey)
            {
                ////"Given an entity with a natural key selector defined in the bootstrapper"
                ////    .Given(() => { });

                ////"And a natural key value"
                ////    .And(() => naturalKey = "key");

                ////"When two instances of that entity are instantiated with that natural key value assigned"
                ////    .When(() =>
                ////    {
                ////        instance1 = new Subject { NaturalKey = naturalKey };
                ////        instance2 = new Subject { NaturalKey = naturalKey };
                ////    });

                ////"Then the first instance is equal to the second instance"
                ////    .Then(() => instance1.Should().Be(instance2));
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

                public string NaturalKey { get; set; }

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
