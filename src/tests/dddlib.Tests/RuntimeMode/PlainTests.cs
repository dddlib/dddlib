// <copyright file="PlainTests.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.RuntimeMode
{
    using dddlib.Runtime;
    using Xunit;

    public class PlainTests
    {
        [Fact(Skip = "Not working yet.")]
        public void Do()
        {
            var assemblyDescriptor = new Configuration();
            var typeAnalyzer = new TypeAnalyzer(assemblyDescriptor);
            var typeDescriptor = typeAnalyzer.GetDescriptor(typeof(Plain.Car));
        }
    }
}
