// <copyright file="DefaultEventMapper.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;

    internal sealed class DefaultEventMapper<TEvent> : IEventMapper<TEvent>
    {
        private readonly TEvent source;

        public DefaultEventMapper(TEvent source)
        {
            // NOTE (Cameron): Cannot use Guardian because there is no class type constraint.
            if (source == null)
            {
                throw new ArgumentNullException(Guard.Expression.Parse(() => source), "Value cannot be null.");
            }

            this.source = source;
        }

        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1118:ParameterMustNotSpanMultipleLines", Justification = "It's fine here.")]
        public T ToEntity<T>() where T : Entity
        {
            var runtimeType = Application.Current.GetEntityType(typeof(T));

            var mapping = default(Func<TEvent, T>);
            if (!runtimeType.Mappings.TryGet(out mapping))
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        @"The entity of type '{0}' has not been configured to reverse map from an event of type '{1}'.
To fix this issue:
- use a bootstrapper to register a reverse mapping for the event.",
                        typeof(T),
                        this.source.GetType()));
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
                        "An exception occurred whilst attempting to map an event of type '{0}' to entity of type '{1}'.\r\nSee inner exception for details.",
                        this.source.GetType(),
                        typeof(T)),
                    ex);
            }
        }

        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1118:ParameterMustNotSpanMultipleLines", Justification = "It's fine here.")]
        public T ToValueObject<T>() where T : ValueObject<T>
        {
            var runtimeType = Application.Current.GetValueObjectType(typeof(T));

            var mapping = default(Func<TEvent, T>);
            if (!runtimeType.Mappings.TryGet(out mapping))
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        @"The value object of type '{0}' has not been configured to reverse map from an event of type '{1}'.
To fix this issue:
- use a bootstrapper to register a reverse mapping for the event.",
                        typeof(T),
                        this.source.GetType()));
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
                        "An exception occurred whilst attempting to map an event of type '{0}' to value object of type '{1}'.\r\nSee inner exception for details.",
                        this.source.GetType(),
                        typeof(T)),
                    ex);
            }
        }
    }
}
