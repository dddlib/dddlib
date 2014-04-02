// <copyright file="NaturalKeyTest.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Acceptance
{
    using System.Diagnostics.CodeAnalysis;
    using dddlib.Configuration;
    using dddlib.Runtime;
    using dddlib.Tests.Sdk;

    [SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1201:ElementsMustAppearInTheCorrectOrder", Justification = "Not here.")]
    internal class NaturalKeyTest : AggregateRootTest<NaturalKeyTest.Subject>
    {
        public class Subject : AggregateRoot
        {
            [NaturalKey]
            public string NaturalKey { get; set; }
        }

        private class BootStrapper : IBootstrap<Subject>
        {
            public void Bootstrap(IConfiguration configure)
            {
            }
        }

        protected override void AssertValid(AggregateRootType aggregateRootType)
        {
        }

        protected override void AssertValid(EntityType entityType)
        {
        }
    }
}
