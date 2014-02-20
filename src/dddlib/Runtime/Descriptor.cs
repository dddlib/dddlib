// <copyright file="Descriptor.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;

    internal abstract class Descriptor
    {
        public void Add(string description, params string[] arguments)
        {
        }

        public void Add(Exception ex, string description, params string[] arguments)
        {
        }
    }
}
