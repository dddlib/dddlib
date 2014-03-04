// <copyright file="TypeAnalyzerTests.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Unit.Runtime
{
    using dddlib.Runtime;
    using Xunit;

    public class TypeAnalyzerTests
    {
        [Fact]
        public void Do()
        {
            // arrange
            var type = typeof(TestAggregate);
            var typeConfiguration = new TypeConfiguration();
            var typeAnalyzer = new TypeAnalyzer();

            // act
            var typeDescriptor = typeAnalyzer.GetDescriptor(type, typeConfiguration);
            
            // assert
            ////typeDescriptor.
        }

        public class TestAggregate : AggregateRoot
        {
        }
    }
}
