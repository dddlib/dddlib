// <copyright file="Wheel.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Support
{
    using System;

    public class Wheel : Entity
    {
        public Wheel(Guid id)
        {
            if (id.Equals(Guid.Empty))
            {
                throw new ArgumentException("Id cannot be an empty GUID.", "id");
            }

            this.Id = id;
        }

        public Guid Id { get; private set; }
    }
}
