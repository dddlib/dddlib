// <copyright file="ValueObjectType.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System.Collections.Generic;

    // TODO (Cameron): This class is nonsense.
    internal class ValueObjectType // where T : ValueObject<T>
    {
        private readonly object equalityComparer;

        ////public IEqualityComparer<object> EqualityComparer { get; internal set; }
        ////public IEqualityComparer<ValueObject<T>> EqualityComparer { get; internal set; }

        public ValueObjectType(object equalityComparer)
        {
            this.equalityComparer = equalityComparer;
        }

        public IEqualityComparer<T> CreateEqualityComparer<T>() 
            where T : ValueObject<T>
        {
            return (this.equalityComparer as IEqualityComparer<T>) ?? new ValueObjectEqualityComparer<T>();
        }
    }
}
