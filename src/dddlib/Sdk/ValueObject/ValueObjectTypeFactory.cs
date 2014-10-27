// <copyright file="ValueObjectTypeFactory.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using dddlib.Sdk;

    internal class ValueObjectTypeFactory : IValueObjectTypeFactory
    {
        public ValueObjectType Create(ValueObjectConfiguration configuration)
        {
            Guard.Against.Null(() => configuration);

            return new ValueObjectType(configuration.RuntimeType, configuration.EqualityComparer);
        }
    }
}
