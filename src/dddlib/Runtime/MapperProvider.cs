// <copyright file="MapperProvider.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using dddlib.Sdk;

    internal class MapperProvider : IMapProvider
    {
        private readonly Mapper mapper;

        public MapperProvider(Mapper mapper)
        {
            this.mapper = mapper;
        }

        public IEventMapper<T> Event<T>(T @event)
        {
            return new EventMapper<T>(@event, this.mapper);
        }

        public IEntityMapper<T> Entity<T>(T entity) where T : Entity
        {
            throw new NotImplementedException();
        }

        public IValueObjectMapper<T> ValueObject<T>(T valueObject) where T : ValueObject<T>
        {
            var runtimeType = Application.Current.GetValueObjectType(valueObject.GetType());

            return new ValueObjectMapper<T>(valueObject, this.mapper);
        }
    }
}
