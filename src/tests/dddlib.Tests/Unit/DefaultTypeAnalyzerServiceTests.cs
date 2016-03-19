// <copyright file="DefaultTypeAnalyzerServiceTests.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Unit
{
    using System;
    using dddlib.Sdk.Configuration.Services.TypeAnalyzer;
    using FluentAssertions;
    using Xunit;

    public class DefaultTypeAnalyzerServiceTests
    {
        [Theory]
        [InlineData(typeof(DefaultInternalConstructorExample))]
        [InlineData(typeof(DefaultPublicConstructorExample))]
        [InlineData(typeof(DefaultPrivateConstructorExample))]
        [InlineData(typeof(DefaultProtectedConstructorExample))]
        public void CanGetUninitializedFactories(Type type)
        {
            // arrange
            var typeAnalyzer = new DefaultTypeAnalyzerService();

            // act
            var uninitializedFactory = typeAnalyzer.GetUninitializedFactory(type);
            var result = uninitializedFactory.DynamicInvoke(null);

            // assert
            result.Should().NotBeNull();
            result.Should().BeOfType(type);
        }

        public class DefaultInternalConstructorExample
        {
            public DefaultInternalConstructorExample(string irrelevant)
            {
            }

            protected internal DefaultInternalConstructorExample()
            {
            }
        }

        public class DefaultPublicConstructorExample
        {
            public DefaultPublicConstructorExample()
            {
            }

            public DefaultPublicConstructorExample(string irrelevant)
            {
            }
        }

        public class DefaultPrivateConstructorExample
        {
            public DefaultPrivateConstructorExample(string irrelevant)
            {
            }

            private DefaultPrivateConstructorExample()
            {
            }
        }

        public class DefaultProtectedConstructorExample
        {
            public DefaultProtectedConstructorExample(string irrelevant)
            {
            }

            protected DefaultProtectedConstructorExample()
            {
            }
        }
    }
}
