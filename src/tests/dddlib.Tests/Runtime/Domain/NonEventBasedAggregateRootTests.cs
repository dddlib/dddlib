// <copyright file="NonEventBasedAggregateRootTests.cs" company="dddlib contributors">
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
    public class NonEventBasedAggregateRootTests
    {
        [Fact]
        public void CreateWithNonEventBasedConfiguration()
        {
            // arrange
            var type = typeof(TestAggregate);
            var typeConfiguration = TypeConfiguration.Create();
            var typeConfigurationProvider = A.Fake<ITypeConfigurationProvider>(o => o.Strict());

            A.CallTo(() => typeConfigurationProvider.GetConfiguration(type)).Returns(typeConfiguration);

            using (new Application(t => typeConfigurationProvider))
            {
                // act (and assert)
                new TestAggregate();
            }
        }

        [Fact]
        public void CreateWithNonPersistableEventBasedConfiguration()
        {
            // arrange
            var type = typeof(TestAggregate);
            var typeConfiguration = TypeConfiguration.Create(t => new DefaultEventDispatcher(t));
            var typeConfigurationProvider = A.Fake<ITypeConfigurationProvider>(o => o.Strict());

            A.CallTo(() => typeConfigurationProvider.GetConfiguration(type)).Returns(typeConfiguration);

            using (new Application(t => typeConfigurationProvider))
            {
                // act (and assert)
                new TestAggregate();
            }
        }

        [Fact]
        public void CreateWithPersistableEventBasedConfiguration()
        {
            // arrange
            var type = typeof(TestAggregate);
            var typeConfiguration = TypeConfiguration.Create(t => new DefaultEventDispatcher(t), () => new TestAggregate());
            var typeConfigurationProvider = A.Fake<ITypeConfigurationProvider>(o => o.Strict());

            A.CallTo(() => typeConfigurationProvider.GetConfiguration(type)).Returns(typeConfiguration);

            using (new Application(t => typeConfigurationProvider))
            {
                // act
                Action action = () => new TestAggregate();
                
                // assert
                action.ShouldThrow<RuntimeException>();
            }
        }

        private class TestAggregate : AggregateRoot
        {
        }
    }
}
