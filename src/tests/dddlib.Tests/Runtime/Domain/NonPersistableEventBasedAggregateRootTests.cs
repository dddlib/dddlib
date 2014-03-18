// <copyright file="NonPersistableEventBasedAggregateRootTests.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Runtime
{
    using System;
    using dddlib.Runtime;
    using FakeItEasy;
    using FluentAssertions;
    using Xunit;

    // for every type of configuration
    public class NonPersistableEventBasedAggregateRootTests
    {
        [Fact(Skip = "I'm not sure that this is the desired behaviour.")]
        public void CreateWithNonEventBasedConfiguration()
        {
            // arrange
            var type = typeof(TestAggregate);
            var typeConfiguration = new TypeConfiguration();
            var typeConfigurationProvider = A.Fake<ITypeConfigurationProvider>(o => o.Strict());

            A.CallTo(() => typeConfigurationProvider.GetConfiguration(type)).Returns(typeConfiguration);

            using (new Application(typeConfigurationProvider, new TypeAnalyzer()))
            {
                // act (and assert)
                new TestAggregate().Value.Should().BeNull();
            }
        }

        [Fact]
        public void CreateWithNonPersistableEventBasedConfiguration()
        {
            // arrange
            var type = typeof(TestAggregate);
            var typeConfiguration = new TypeConfiguration { EventDispatcher = new DefaultEventDispatcher(typeof(TestAggregate)) };
            var typeConfigurationProvider = A.Fake<ITypeConfigurationProvider>(o => o.Strict());

            A.CallTo(() => typeConfigurationProvider.GetConfiguration(type)).Returns(typeConfiguration);

            using (new Application(typeConfigurationProvider, new TypeAnalyzer()))
            {
                // act (and assert)
                new TestAggregate().Value.Should().NotBeNull();
            }
        }

        [Fact(Skip = "Not sure this is still relevant")]
        public void CreateWithPersistableEventBasedConfiguration()
        {
            // arrange
            var type = typeof(TestAggregate);
            var typeConfiguration = new TypeConfiguration { EventDispatcher = new DefaultEventDispatcher(typeof(TestAggregate)), AggregateRootFactory = () => new TestAggregate() };
            var typeConfigurationProvider = A.Fake<ITypeConfigurationProvider>(o => o.Strict());

            A.CallTo(() => typeConfigurationProvider.GetConfiguration(type)).Returns(typeConfiguration);

            using (new Application(typeConfigurationProvider, new TypeAnalyzer()))
            {
                // act
                Action action = () => new TestAggregate();

                // assert
                action.ShouldThrow<RuntimeException>();
            }
        }

        private class TestAggregate : AggregateRoot
        {
            public TestAggregate()
            {
                this.Apply(new object());
            }

            public object Value { get; private set; }

            private void Handle(object @event)
            {
                this.Value = @event;
            }
        }
    }
}
