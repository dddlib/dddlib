// <copyright file="Vector.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Support
{
    using System.Collections.Generic;

    public sealed class Vector : ValueObject<Vector>
    {
        public Vector(int x, int y)
        {
            this.X = x;
            this.Y = y;
        }

        public int X { get; private set; }

        public int Y { get; private set; }

        protected override IEnumerable<object> GetValue()
        {
            yield return this.X;
            yield return this.Y;
        }
    }
}
