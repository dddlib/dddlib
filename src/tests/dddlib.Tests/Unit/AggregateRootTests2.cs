// <copyright file="AggregateRootTests2.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Acceptance
{
    using dddlib.Configuration;
    using dddlib.Runtime;
    using FluentAssertions;
    using Xbehave;

    public class AggregateRootTests2
    {
        [Background]
        public void Background()
        {
            "Given a new application"
                .f(context => new Application().Using(context));
        }

        [Scenario]
        public void CanInstantiate(string naturalKeyValue)
        {
            var aggregateRoot = default(TestAggregateRoot);

            "Given a natural key value"
                .f(() => naturalKeyValue = "key");

            "When an aggregate root is instantiated with the natural key value"
                .f(() => aggregateRoot = new TestAggregateRoot(new Key { Value = naturalKeyValue }));

            "Then the aggregate root key should be the natural key value"
                .f(() => aggregateRoot.Key.Should().Be(naturalKeyValue));

            "And"
                .f(() => aggregateRoot.GetUncommittedEvents().Should().ContainSingle(x => x.As<Key>().Value == naturalKeyValue));
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
            public TestAggregateRoot(Key key)
            {
                this.Apply(key);
            }

            // uninitialized
            internal TestAggregateRoot()
            {
            }

            [NaturalKey] // (EqualityComparer = typeof(StringComparer))]
            public string Key { get; private set; }

            private void Handle(Key key)
            {
                this.Key = key.Value;
            }
        }

        private class Key
        {
            public string Value { get; set; }
        }
    }
}
