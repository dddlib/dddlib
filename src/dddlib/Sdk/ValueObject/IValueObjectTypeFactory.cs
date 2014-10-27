// <copyright file="IValueObjectTypeFactory.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    internal interface IValueObjectTypeFactory
    {
        ValueObjectType Create(ValueObjectConfiguration configuration);
    }
}
