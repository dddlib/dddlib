// <copyright file="PlainTests.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.RuntimeMode
{
    using System;
    using dddlib.Runtime;
    using dddlib.Runtime.Configuration;
    using FakeItEasy;
    using FluentAssertions;
    using Xunit;

    public class PlainTests
    {
        [Fact]
        public void CreateNonEventBasedAggregateRoot()
        {
            var type = typeof(TestAggregate);
            var typeConfiguration = new TypeConfiguration();
            var typeConfigurationProvider = A.Fake<ITypeConfigurationProvider>(o => o.Strict());

            A.CallTo(() => typeConfigurationProvider.GetConfiguration(type)).Returns(typeConfiguration);

            using (new Application(typeConfigurationProvider))
            {
                Action action = () => new TestAggregate();
                action.ShouldNotThrow();
            }
        }

        [Fact]
        public void CreateEventBasedTransientAggregateRoot()
        {
            var type = typeof(TestAggregate);
            var typeConfiguration = new TypeConfiguration(new DefaultEventDispatcher(typeof(TestAggregate)));
            var typeConfigurationProvider = A.Fake<ITypeConfigurationProvider>(o => o.Strict());

            A.CallTo(() => typeConfigurationProvider.GetConfiguration(type)).Returns(typeConfiguration);

            using (new Application(typeConfigurationProvider))
            {
                Action action = () => new TestAggregate();
                action.ShouldNotThrow();
            }
        }

        [Fact]
        public void CreateEventBasedAggregateRoot()
        {
            var type = typeof(TestAggregate);
            var typeConfiguration = new TypeConfiguration(new DefaultEventDispatcher(typeof(TestAggregate)), () => new TestAggregate());
            var typeConfigurationProvider = A.Fake<ITypeConfigurationProvider>(o => o.Strict());

            A.CallTo(() => typeConfigurationProvider.GetConfiguration(type)).Returns(typeConfiguration);

            using (new Application(typeConfigurationProvider))
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
