// <copyright file="ITypeAnalyzerService.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk.Configuration.Model
{
    using System;

    internal interface ITypeAnalyzerService
    {
        bool IsValidAggregateRoot(Type runtimeType);

        bool IsValidEntity(Type runtimeType);

        bool IsValidValueObject(Type runtimeType);

        bool IsValidProperty(Type runtimeType, string propertyName, Type propertyType);

        NaturalKey GetNaturalKey(Type runtimeType);
    }
}