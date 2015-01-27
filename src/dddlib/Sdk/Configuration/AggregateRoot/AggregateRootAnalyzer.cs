// <copyright file="AggregateRootAnalyzer.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using dddlib.Sdk;

    internal class AggregateRootAnalyzer : IAggregateRootConfigurationProvider
    {
        public AggregateRootConfiguration GetConfiguration(Type type)
        {
            return new AggregateRootConfiguration
            {
                UninitializedFactory = null, // cannot specify factory on type
                ////ApplyMethodName = "Handle", // default
            };
        }
    }
}
