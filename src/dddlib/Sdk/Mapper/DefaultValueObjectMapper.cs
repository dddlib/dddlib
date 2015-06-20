// <copyright file="DefaultValueObjectMapper.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Globalization;

    internal sealed class DefaultValueObjectMapper<TValueObject> : IValueObjectMapper<TValueObject>
        where TValueObject : ValueObject<TValueObject>
    {
        private readonly TValueObject source;

        public DefaultValueObjectMapper(TValueObject source)
        {
            Guard.Against.Null(() => source);

            this.source = source;
        }

        public T ToEvent<T>() where T : new()
        {
            var @event = new T();
            this.ToEvent(@event);
            return @event;
        }

        public T ToEvent<T>(T @event)
        {
            var runtimeType = Application.Current.GetValueObjectType(this.source.GetType());

            var mapping = default(Action<TValueObject, T>);
            if (!runtimeType.Mappings.TryGet(out mapping))
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "The value object of type '{0}' has not been configured to map to an event of type '{1}'.",
                        this.source.GetType(),
                        typeof(T)));
            }

            try
            {
                mapping.Invoke(this.source, @event);
            }
            catch (Exception ex)
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture, 
                        "An exception occurred when mapping a value object of type '{0}' to event of type '{1}'.", 
                        this.source.GetType(), 
                        typeof(T)),
                    ex);
            }

            return @event;
        }
    }
}
