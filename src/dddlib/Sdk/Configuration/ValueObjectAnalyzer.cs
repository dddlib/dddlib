// <copyright file="ValueObjectAnalyzer.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk.Configuration
{
    using System;
    using dddlib.Sdk;
    using dddlib.Sdk.Configuration;

    internal class ValueObjectAnalyzer : IValueObjectConfigurationProvider
    {
        public ValueObjectConfiguration GetConfiguration(Type type)
        {
            return new ValueObjectConfiguration();
        }
    }
}
