// <copyright file="IValueObjectTypeFactory.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk
{
    internal interface IValueObjectTypeFactory
    {
        ValueObjectType Create(ValueObjectConfiguration configuration);
    }
}
