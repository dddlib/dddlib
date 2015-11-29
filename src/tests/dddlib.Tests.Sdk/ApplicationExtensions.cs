// <copyright file="ApplicationExtensions.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Sdk
{
    using System;
    using dddlib.Runtime;
    using dddlib.Sdk.Configuration.Model;

    public static class ApplicationExtensions
    {
        public static AggregateRootType GetAggregateRootType(this Application application, Type type)
        {
            return application.GetAggregateRootType(type);
        }

        ////public static ValueObjectType GetValueObjectType(this Application application, Type type)
        ////{
        ////    return application.GetValueObjectType(type);
        ////}

        public static ValueObjectType GetValueObjectType(this Application application, Type type)
        {
            return application.GetValueObjectType(type);
        }
    }
}
