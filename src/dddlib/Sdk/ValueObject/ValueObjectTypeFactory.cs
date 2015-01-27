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

            var equalityComparer = configuration.EqualityComparer ?? CreateEqualityComparer(configuration.RuntimeType);

            return new ValueObjectType(configuration.RuntimeType, equalityComparer, configuration.Mappings ?? new MappingCollection());
        }

        private static object CreateEqualityComparer(Type type)
        {
            return Activator.CreateInstance(typeof(ValueObjectEqualityComparer<>).MakeGenericType(type));
        }
    }
}
