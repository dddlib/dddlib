// <copyright file="DefaultMapperProvider.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    // TODO (Cameron): Make public and provide override methods with null checks.
    internal class DefaultMapperProvider : IMapperProvider
    {
        public IEventMapper<T> Event<T>(T @event)
        {
            return new DefaultEventMapper<T>(@event);
        }

        public IEntityMapper<T> Entity<T>(T entity) where T : Entity
        {
            return new DefaultEntityMapper<T>(entity);
        }

        public IValueObjectMapper<T> ValueObject<T>(T valueObject) where T : ValueObject<T>
        {
            return new DefaultValueObjectMapper<T>(valueObject);
        }
    }
}
