// <copyright file="EventMapper.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;

    internal class EventMapper<TEvent> : IEventMapper<TEvent>
    {
        private readonly TEvent source;
        ////private readonly Mapper mapper;

        public EventMapper(TEvent source)
        {
            this.source = source;
            ////this.mapper = mapper;
        }

        public T ToEntity<T>() where T : Entity
        {
            throw new NotImplementedException();

            ////var map = this.mapper.GetFuncMap<TEvent, T>();
            ////return map(this.source);
        }

        public T ToValueObject<T>() where T : ValueObject<T>
        {
            var runtimeType = Application.Current.GetValueObjectType(typeof(T));

            var mapping = default(Func<TEvent, T>);
            if (!runtimeType.Mappings.TryGet(out mapping))
            {
                throw new RuntimeException("Map doesn't exist.");
            }

            ////var map = this.mapper.GetFuncMap<TEvent, T>();
            return mapping.Invoke(this.source);
        }
    }
}
