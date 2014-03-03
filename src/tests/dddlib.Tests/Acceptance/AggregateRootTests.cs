// <copyright file="AggregateRootTests.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Acceptance
{
    using dddlib.Runtime;
    using FluentAssertions;
    using Xbehave;

    public class AggregateRootTests
    {
        [Background]
        public void Background()
        {
            "Given a new application"
                .Given(() => new Application().Using());
        }

        [Scenario]
        public void CanInstantiate(string naturalKeyValue)
        {
            var aggregateRoot = default(TestAggregateRoot);

            "Given a natural key value"
                .Given(() => naturalKeyValue = "key");

            "When an aggregate root is instantiated with the natural key value"
                .When(() => aggregateRoot = new TestAggregateRoot(naturalKeyValue));

            "The the aggregate root key should be the natural key value"
                .Then(() => aggregateRoot.Key.Should().Be(naturalKeyValue));

            "And"
                .And(() => aggregateRoot.GetUncommittedEvents().Should().ContainSingle(x => x as string == naturalKeyValue));
        }

        private class Bootstrapper : IBootstrapper
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.AggregateRoot<TestAggregateRoot>().ToReconstituteUsing(() => new TestAggregateRoot());
            }
        }

        private class TestAggregateRoot : AggregateRoot
        {
            public TestAggregateRoot(string key)
            {
                this.Apply(key);
            }

            // uninitialized
            internal TestAggregateRoot()
            {
            }

            [NaturalKey] // (EqualityComparer = typeof(StringComparer))]
            public string Key { get; private set; }

            private void Handle(string key)
            {
                this.Key = key;
            }
        }
    }
}
