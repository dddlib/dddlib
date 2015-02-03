// <copyright file="ValueObjectTypeFactory.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk
{
    using System;
    using dddlib.Sdk.Configuration;

    internal class ValueObjectTypeFactory
    {
        public ValueObjectType Create(Type type, ValueObjectConfiguration configuration)
        {
            Guard.Against.Null(() => configuration);

            var equalityComparer = configuration.EqualityComparer ?? CreateEqualityComparer(type);

            return new ValueObjectType(type, equalityComparer, configuration.Mappings ?? new MapperCollection());
        }

        private static object CreateEqualityComparer(Type type)
        {
            return Activator.CreateInstance(typeof(DefaultValueObjectEqualityComparer<>).MakeGenericType(type));
        }
    }
}
