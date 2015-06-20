// <copyright file="DefaultEntityMapper.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Globalization;

    internal sealed class DefaultEntityMapper<TEntity> : IEntityMapper<TEntity>
        where TEntity : Entity
    {
        private readonly TEntity source;

        public DefaultEntityMapper(TEntity source)
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

        public void ToEvent<T>(T @event)
        {
            var runtimeType = Application.Current.GetEntityType(this.source.GetType());

            var mapping = default(Action<TEntity, T>);
            if (!runtimeType.Mappings.TryGet(out mapping))
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "The entity of type '{0}' has not been configured to map to an event of type '{1}'.",
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
                        "An exception occurred when mapping an entity of type '{0}' to event of type '{1}'.",
                        this.source.GetType(),
                        typeof(T)),
                    ex);
            }
        }
    }
}
