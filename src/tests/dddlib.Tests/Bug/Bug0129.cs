// <copyright file="Bug0129.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Bug
{
    using System.Collections.Generic;
    using Xunit;

    public class Bug0129
    {
        [Fact]
        public void ShouldNotThrow()
        {
            new CommitChain(
                new[]
                {
                    new CommitChain.Element
                    {
                        Commit = new Commit()
                    },
                });
        }

        public class CommitChain : ValueObject<CommitChain>
        {
            public CommitChain(IEnumerable<Element> elements)
            {
                this.Elements = new List<Element>(elements);
            }

            public IEnumerable<Element> Elements { get; set; }

            public class Element : ValueObject<Element>
            {
                public Commit Commit { get; set; }
            }
        }

        public class Commit : Entity
        {
            public string Id { get; set; }
        }
    }
}
