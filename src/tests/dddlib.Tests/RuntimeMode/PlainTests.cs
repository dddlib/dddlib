// <copyright file="PlainTests.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.RuntimeMode
{
    using System;
    using dddlib.Runtime;
    using FakeItEasy;
    using FluentAssertions;
    using Xunit;

    public class PlainTests
    {
        [Fact]
        public void CreatePlainAggregateRoot()
        {
            var type = typeof(TestAggregate);
            var typeConfiguration = TypeConfiguration.Create();
            var typeConfigurationProvider = A.Fake<ITypeConfigurationProvider>(o => o.Strict());

            A.CallTo(() => typeConfigurationProvider.GetConfiguration(type)).Returns(typeConfiguration);

            using (new Application(t => typeConfigurationProvider))
            {
                Action action = () => new TestAggregate();
                action.ShouldNotThrow();
            }
        }

        [Fact]
        public void CreateEventSourcingWithoutPersistenceAggregateRoot()
        {
            var type = typeof(TestAggregate);
            var typeConfiguration = TypeConfiguration.Create(t => new DefaultEventDispatcher(t));
            var typeConfigurationProvider = A.Fake<ITypeConfigurationProvider>(o => o.Strict());

            A.CallTo(() => typeConfigurationProvider.GetConfiguration(type)).Returns(typeConfiguration);

            using (new Application(t => typeConfigurationProvider))
            {
                Action action = () => new TestAggregate();
                action.ShouldNotThrow();
            }
        }

        [Fact]
        public void CreateEventSourcingAggregateRoot()
        {
            var type = typeof(TestAggregate);
            var typeConfiguration = TypeConfiguration.Create(t => new DefaultEventDispatcher(t), () => new TestAggregate());
            var typeConfigurationProvider = A.Fake<ITypeConfigurationProvider>(o => o.Strict());

            A.CallTo(() => typeConfigurationProvider.GetConfiguration(type)).Returns(typeConfiguration);

            using (new Application(t => typeConfigurationProvider))
            {
                Action action = () => new TestAggregate();
                action.ShouldThrow<RuntimeException>();
            }
        }

        private class TestAggregate : AggregateRoot
        {
        }
    }
}
