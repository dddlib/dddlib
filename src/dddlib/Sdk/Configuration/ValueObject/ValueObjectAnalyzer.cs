// <copyright file="ValueObjectAnalyzer.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using dddlib.Sdk;

    internal class ValueObjectAnalyzer : IValueObjectConfigurationProvider
    {
        public ValueObjectConfiguration GetConfiguration(Type type)
        {
            return new ValueObjectConfiguration();
        }
    }
}
