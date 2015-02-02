// <copyright file="EventMapper.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Globalization;

    internal sealed class EventMapper<TEvent> : IEventMapper<TEvent>
    {
        private readonly TEvent source;

        public EventMapper(TEvent source)
        {
            // NOTE (Cameron): Cannot use Guardian because there is no class type constraint.
            if (source == null)
            {
                throw new ArgumentNullException("source", "Value cannot be null.");
            }

            this.source = source;
        }

        public T ToEntity<T>() where T : Entity
        {
            var runtimeType = Application.Current.GetEntityType(typeof(T));

            var mapping = default(Func<TEvent, T>);
            if (!runtimeType.Mappings.TryGet(out mapping))
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "The entity of type '{0}' has not been configured to reverse map from an event of type '{1}'.",
                        this.source.GetType(),
                        typeof(T)));
            }

            try
            {
                return mapping.Invoke(this.source);
            }
            catch (Exception ex)
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "An exception occurred when mapping event of type '{0}' to entity of type '{1}'.",
                        this.source.GetType(),
                        typeof(T)),
                    ex);
            }
        }

        public T ToValueObject<T>() where T : ValueObject<T>
        {
            var runtimeType = Application.Current.GetValueObjectType(typeof(T));

            var mapping = default(Func<TEvent, T>);
            if (!runtimeType.Mappings.TryGet(out mapping))
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "The value object of type '{0}' has not been configured to reverse map from an event of type '{1}'.",
                        this.source.GetType(),
                        typeof(T)));
            }

            try
            {
                return mapping.Invoke(this.source);
            }
            catch (Exception ex)
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "An exception occurred when mapping event of type '{0}' to value object of type '{1}'.",
                        this.source.GetType(),
                        typeof(T)),
                    ex);
            }
        }
    }
}
