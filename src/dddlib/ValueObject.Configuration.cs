// <copyright file="ValueObject.Configuration.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib
{
    using System.Collections.Generic;
    using dddlib.Sdk.Configuration.Model;

    /// <content>
    /// Represents a value object.
    /// </content>
    public abstract partial class ValueObject<T>
    {
        internal ValueObject(IEqualityComparer<T> equalityComparer)
            : this(@this => new TypeInformation(equalityComparer))
        {
        }

        private class TypeInformation
        {
            public TypeInformation(ValueObjectType valueObjectType)
            {
                Guard.Against.Null(() => valueObjectType);

                this.EqualityComparer = (IEqualityComparer<T>)valueObjectType.EqualityComparer;
            }

            public TypeInformation(IEqualityComparer<T> equalityComparer)
            {
                this.EqualityComparer = equalityComparer;
            }

            public IEqualityComparer<T> EqualityComparer { get; private set; }
        }
    }
}
