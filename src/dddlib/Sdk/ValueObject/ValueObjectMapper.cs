// <copyright file="ValueObjectMapper.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk
{
    using System;
    using dddlib.Runtime;

    internal class ValueObjectMapper<T> : IValueObjectMapper<T>
        where T : ValueObject<T>
    {
        public ValueObjectMapper(ValueObjectType runtimeType)
        {
        }

        public TEvent ToEvent<TEvent>() where TEvent : new()
        {
            throw new NotImplementedException();
        }

        public void ToEvent<TEvent>(TEvent @event)
        {
            throw new NotImplementedException();
        }
    }
}
