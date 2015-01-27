// <copyright file="ValueObjectMapper.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk
{
    using System;
    using dddlib.Runtime;

    internal class ValueObjectMapper<TValueObject> : IValueObjectMapper<TValueObject>
        where TValueObject : ValueObject<TValueObject>
    {
        private readonly TValueObject source;

        public ValueObjectMapper(TValueObject source)
        {
            this.source = source;
        }

        public T ToEvent<T>() where T : new()
        {
            var @event = new T();
            this.ToEvent(@event);
            return @event;
        }

        public void ToEvent<T>(T @event)
        {
            var runtimeType = Application.Current.GetValueObjectType(this.source.GetType());

            var mapping = default(Action<TValueObject, T>);
            if (!runtimeType.Mappings.TryGet(out mapping))
            {
                throw new RuntimeException("Map doesn't exist.");
            }

            mapping.Invoke(this.source, @event);
        }
    }
}
