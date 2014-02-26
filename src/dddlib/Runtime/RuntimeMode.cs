// <copyright file="RuntimeMode.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    internal enum RuntimeMode
    {
        EventSourcing = 0,
        EventSourcingWithoutPersistence = 1,
        Plain = 2,
    }
}
