// <copyright file="TypeConfigurationTests.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Unit.Runtime.Configuration
{
    using dddlib.Runtime;
    using dddlib.Runtime.Configuration;
    using FluentAssertions;
    using Xunit;

    public class TypeConfigurationTests
    {
        [Fact]
        public void CreateTypeConfigurationWithNoArguments()
        {
            // arrange
            var typeConfiguration = new TypeConfiguration();

            // assert
            typeConfiguration.AggregateRootFactory.Should().BeNull();
            typeConfiguration.EventDispatcher.Should().BeNull();
        }
    }
}
