// <copyright file="DefaultValueObjectMapper.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Diagnostics.CodeAnalysis;
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

        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1118:ParameterMustNotSpanMultipleLines", Justification = "It's fine here.")]
        public T ToEvent<T>(T @event)
        {
            var runtimeType = Application.Current.GetValueObjectType(this.source.GetType());

            var mapping = default(Action<TValueObject, T>);
            if (!runtimeType.Mappings.TryGet(out mapping))
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        @"The value object of type '{0}' has not been configured to map to an event of type '{1}'.
To fix this issue:
- use a bootstrapper to register a mapping for the event.",
                        this.source.GetType(),
                        typeof(T)))
                {
                    HelpLink = "https://github.com/dddlib/dddlib/wiki/Aggregate-Root-Value-Object-Mapping",
                };
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
                        "An exception occurred whilst attempting to map a value object of type '{0}' to an event of type '{1}'.\r\nSee inner exception for details.",
                        this.source.GetType(), 
                        typeof(T)),
                    ex);
            }

            return @event;
        }
    }
}
