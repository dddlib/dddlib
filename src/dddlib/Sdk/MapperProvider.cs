// <copyright file="MapperProvider.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk
{
    using System;
    using dddlib.Runtime;

    internal class MapperProvider : IMapProvider
    {
        public IEventMapper<T> Event<T>(T @event)
        {
            return new EventMapper<T>(@event);
        }

        public IEntityMapper<T> Entity<T>(T entity) where T : Entity
        {
            throw new NotImplementedException();
        }

        public IValueObjectMapper<T> ValueObject<T>(T valueObject) where T : ValueObject<T>
        {
            return new ValueObjectMapper<T>(valueObject);
        }
    }
}
