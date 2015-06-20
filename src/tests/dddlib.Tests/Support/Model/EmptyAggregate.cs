// <copyright file="EmptyAggregate.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Support.Model
{
    public class EmptyAggregate : AggregateRoot
    {
        [NaturalKey]
        public string NaturalKey
        {
            get { return string.Empty; }
        }
    }
}
