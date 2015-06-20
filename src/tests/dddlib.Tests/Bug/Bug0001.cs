// <copyright file="Bug0001.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Bug
{
    using System;
    using Xunit;

    public class Bug0001
    {
        [Fact]
        public void ShouldNotThrow()
        {
            new Task(Guid.NewGuid());
        }

        public class Task : Entity
        {
            public Task(Guid id)
            {
                this.Id = id;
            }

            [NaturalKey]
            public Guid Id { get; set; }
        }
    }
}
