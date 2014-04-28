// <copyright file="ITypeFactory.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;

    internal interface ITypeFactory<T>
    {
        T Create(Type type);
    }
}
