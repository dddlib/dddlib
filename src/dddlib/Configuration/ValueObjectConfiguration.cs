// <copyright file="ValueObjectConfiguration.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Configuration
{
    using System;

    internal class ValueObjectConfiguration<T> : IConfigureValueObject<T>
        where T : ValueObject<T>
    {
        public IConfigureValueObject<T> ToMapAs<TOut>(Func<T, TOut> mapping)
        {
            return this;
        }
    }
}
