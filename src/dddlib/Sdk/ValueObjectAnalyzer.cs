// <copyright file="ValueObjectAnalyzer.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;

    internal class ValueObjectAnalyzer
    {
        public ValueObjectConfiguration GetConfiguration(Type type)
        {
            return new ValueObjectConfiguration();
        }
    }
}
