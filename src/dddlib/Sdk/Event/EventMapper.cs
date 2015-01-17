// <copyright file="EventMapper.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    internal class EventMapper<TEvent> : IEventMapper<TEvent>
    {
        private readonly TEvent source;
        private readonly Mapper mapper;

        public EventMapper(TEvent source, Mapper mapper)
        {
            this.source = source;
            this.mapper = mapper;
        }

        public T ToEntity<T>() where T : Entity
        {
            var map = this.mapper.GetFuncMap<TEvent, T>();
            return map(this.source);
        }

        public T ToValueObject<T>() where T : ValueObject<T>
        {
            var map = this.mapper.GetFuncMap<TEvent, T>();
            return map(this.source);
        }
    }
}
