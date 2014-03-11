// <copyright file="ITypeAnalyzer.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;

    internal interface ITypeAnalyzer<T> where T : RuntimeType
    {
        T Get(Type type, Configuration<T> configuration);
    }

    internal class Configuration<T> where T : RuntimeType
    {
    }
}
