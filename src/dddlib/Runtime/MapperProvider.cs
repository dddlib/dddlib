// <copyright file="MapperProvider.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;

    internal sealed class MapperProvider : IMapperProvider
    {
        public IEventMapper<T> Event<T>(T @event)
        {
            return new EventMapper<T>(@event);
        }

        public IEntityMapper<T> Entity<T>(T entity) where T : Entity
        {
            return new EntityMapper<T>(entity);
        }

        public IValueObjectMapper<T> ValueObject<T>(T valueObject) where T : ValueObject<T>
        {
            return new ValueObjectMapper<T>(valueObject);
        }
    }
}
