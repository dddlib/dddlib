// <copyright file="ITypeAnalyzer.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    internal interface ITypeAnalyzer<T> where T : RuntimeType
    {
        T Get(TypeAnalyzer type, IConfiguration<T> configuration);
    }

    internal interface IConfiguration<T> where T : RuntimeType
    {
    }
}
