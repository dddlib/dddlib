// <copyright file="ValueObjectTypeFactory.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk
{
    using System;
    using dddlib.Sdk.Configuration;

    internal class ValueObjectTypeFactory
    {
        public ValueObjectType Create(ValueObjectConfiguration configuration)
        {
            Guard.Against.Null(() => configuration);

            var equalityComparer = configuration.EqualityComparer ?? CreateEqualityComparer(configuration.RuntimeType);

            return new ValueObjectType(configuration.RuntimeType, equalityComparer, configuration.Mappings ?? new MapperCollection());
        }

        private static object CreateEqualityComparer(Type type)
        {
            return Activator.CreateInstance(typeof(DefaultValueObjectEqualityComparer<>).MakeGenericType(type));
        }
    }
}
