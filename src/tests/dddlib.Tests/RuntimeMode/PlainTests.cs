// <copyright file="PlainTests.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.RuntimeMode
{
    using dddlib.Runtime;
    using Xunit;

    public class PlainTests
    {
        [Fact(Skip = "Doesn't work yet.")]
        public void Do()
        {
            var configuration = new TypeConfiguration();
            configuration.SetEventDispatcherFactory(type => new DefaultEventDispatcher(type));

            using (new Application())
            {
                var aggregate = new TestAggregate();
            }

            ////var typeAnalyzer = new TypeAnalyzer(configuration);
            ////var typeDescriptor = typeAnalyzer.GetDescriptor(typeof(Plain.Car));
        }

        private class TestAggregate : AggregateRoot
        {
        }
    }
}
