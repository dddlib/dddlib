// <copyright file="PersistableEventBasedAggregateRootTests.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Runtime
{
    using System.Linq;
    using dddlib.Runtime;
    using FakeItEasy;
    using FluentAssertions;
    using Xunit;

    // for every type of configuration
    public class PersistableEventBasedAggregateRootTests
    {
        [Fact(Skip = "I'm not sure that this is the desired behaviour.")]
        public void CreateWithNonEventBasedConfiguration()
        {
            // arrange
            var naturalKey = "key";
            var type = typeof(TestAggregate);
            var typeConfiguration = new TypeConfiguration();
            var typeConfigurationProvider = A.Fake<ITypeConfigurationProvider>(o => o.Strict());

            A.CallTo(() => typeConfigurationProvider.GetConfiguration(type)).Returns(typeConfiguration);

            using (new Application(typeConfigurationProvider))
            {
                // act
                var aggregate = new TestAggregate(naturalKey);
                var uncommittedEvents = aggregate.GetUncommittedEvents().ToArray();

                // assert
                uncommittedEvents.Should().BeEmpty();                            // cannot persist
                aggregate.Key.Should().BeNull();                                 // cannot apply
                aggregate.Should().Be(new TestAggregate(naturalKey));            // can equate
            }
        }

        [Fact]
        public void CreateWithNonPersistableEventBasedConfiguration()
        {
            // arrange
            var naturalKey = "key";
            var type = typeof(TestAggregate);
            var typeConfiguration = new TypeConfiguration { EventDispatcher = new DefaultEventDispatcher(typeof(TestAggregate)) };
            var typeConfigurationProvider = A.Fake<ITypeConfigurationProvider>(o => o.Strict());

            A.CallTo(() => typeConfigurationProvider.GetConfiguration(type)).Returns(typeConfiguration);

            using (new Application(typeConfigurationProvider))
            {
                // act
                var aggregate = new TestAggregate(naturalKey);
                var uncommittedEvents = aggregate.GetUncommittedEvents().ToArray();

                // assert
                uncommittedEvents.Should().BeEmpty();                            // cannot persist
                aggregate.Key.Should().Be(naturalKey);                           // can apply
                aggregate.Should().Be(new TestAggregate(naturalKey));            // can equate
            }
        }

        [Fact]
        public void CreateWithPersistableEventBasedConfiguration()
        {
            // arrange
            var naturalKey = "key";
            var type = typeof(TestAggregate);
            var typeConfiguration = new TypeConfiguration { EventDispatcher = new DefaultEventDispatcher(typeof(TestAggregate)), AggregateRootFactory = () => new TestAggregate() };
            var typeConfigurationProvider = A.Fake<ITypeConfigurationProvider>(o => o.Strict());

            A.CallTo(() => typeConfigurationProvider.GetConfiguration(type)).Returns(typeConfiguration);

            using (new Application(typeConfigurationProvider))
            {
                // act
                var aggregate = new TestAggregate(naturalKey);
                var uncommittedEvents = aggregate.GetUncommittedEvents().ToArray();
                
                // assert
                uncommittedEvents.Should().ContainInOrder(new[] { naturalKey }); // can persist
                aggregate.Key.Should().Be(naturalKey);                           // can apply
                aggregate.Should().Be(new TestAggregate(naturalKey));            // can equate
            }
        }

        private class TestAggregate : AggregateRoot
        {
            public TestAggregate(string key)
            {
                this.Apply(key);
            }

            // uninitialized
            internal TestAggregate()
            {
            }

            [NaturalKey] // (EqualityComparer = typeof(StringComparer))]
            public object Key { get; private set; }

            private void Handle(string key)
            {
                this.Key = key;
            }
        }
    }
}
