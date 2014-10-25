// <copyright file="ValueObjectMapper.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk
{
    using dddlib.Runtime;

    internal class ValueObjectMapper<TValueObject> : IValueObjectMapper<TValueObject>
        where TValueObject : ValueObject<TValueObject>
    {
        private readonly TValueObject source;
        private readonly Mapper mapper;

        public ValueObjectMapper(TValueObject source, Mapper mapper)
        {
            this.source = source;
            this.mapper = mapper;
        }

        public T ToEvent<T>() where T : new()
        {
            var @event = new T();
            this.ToEvent(@event);
            return @event;
        }

        public void ToEvent<T>(T @event)
        {
            var map = this.mapper.GetActionMap<TValueObject, T>();
            map(this.source, @event);
        }
    }
}
