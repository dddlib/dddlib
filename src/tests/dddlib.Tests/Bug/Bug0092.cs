// <copyright file="Bug0092.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Bug
{
    using System;
    using FluentAssertions;
    using Xunit;

    public class Bug0092
    {
        [Fact]
        public void RespectLifecycle()
        {
            // arrange
            var subject = new Subject();
            subject.EndLifecycle();

            // act
            Action action = () => subject.EndLifecycle();

            // assert
            action.ShouldThrow<BusinessException>();
        }

        public class Subject : AggregateRoot
        {
            public new void EndLifecycle()
            {
                base.EndLifecycle();
            }
        }
    }
}
