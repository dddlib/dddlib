// <copyright file="AggregateRootTests.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests
{
    using System;
    using FluentAssertions;
    using Xunit;

    public class AggregateRootTests
    {
        [Fact]
        public void CanInstantiateEmptyAggregate()
        {
            // act
            new EmptyAggregate();
        }

        [Fact]
        public void CanApplyHandledChangeToAggregate()
        {
            // arrange
            var aggregate = new ChangeableAggregate();
            var @event = new SomethingHappened();

            // act
            aggregate.ApplyEvent(@event);

            // assert
            aggregate.Change.Should().Be(@event);
        }

        [Fact]
        public void CanApplyMishandledChangeToAggregateWithoutEffectOrException()
        {
            // arrange
            var aggregate = new ChangeableAggregate();
            var @event = new SomethingElseHappened();

            // act
            aggregate.ApplyEvent(@event);

            // assert
            aggregate.Change.Should().BeNull();
        }
        
        [Fact]
        public void CanApplyHandledInheritedChangeToAggregateWithoutEffectOrException()
        {
            // arrange
            var aggregate = new ChangeableAggregate();
            var @event = new SomethingWierdHappened();

            // act
            aggregate.ApplyEvent(@event);

            // assert
            aggregate.Change.Should().BeNull();
        }

        [Fact]
        public void CanApplyUnhandledChangeToAggregateWithoutEffectOrException()
        {
            // arrange
            var aggregate = new ChangeableAggregate();
            var @event = new SomethingNeverHappened();

            // act
            aggregate.ApplyEvent(@event);

            // assert
            aggregate.Change.Should().BeNull();
        }

        [Fact]
        public void CanApplyInvalidChangeToAggregateWithoutEffectOrException()
        {
            // arrange
            var aggregate = new BadAggregate();
            var @event = 1;

            // act
            aggregate.ApplyEvent(@event);

            // assert
            aggregate.Change.Should().BeNull();
        }

        [Fact]
        public void CanApplyOtherInvalidChangeToAggregateWithoutEffectOrException()
        {
            // arrange
            var aggregate = new BadAggregate();
            int? @event = 1;

            // act
            aggregate.ApplyEvent(@event);

            // assert
            aggregate.Change.Should().BeNull();
        }

        [Fact]
        public void CannotApplyMultipleHandledChangeToAggregate()
        {
            // arrange
            Action action = () => new BrokenAggregate();

            // assert
            action.ShouldThrow<InvalidOperationException>();
        }

        [Fact]
        public void CanDestroy()
        {
        }

        private class EmptyAggregate : AggregateRoot
        {
        }

        private class ChangeableAggregate : AggregateRoot
        {
            public object Change { get; protected set; }

            public void ApplyEvent(object change)
            {
                this.ApplyChange(change);
            }

            private void Apply(SomethingHappened @event)
            {
                this.Change = @event;
            }

            private void Apply(SomethingElseHappened @event, int count)
            {
                this.Change = @event;
            }
        }

        private class BrokenAggregate : ChangeableAggregate
        {
            private void Apply(SomethingHappened @event)
            {
            }
        }

        private class BadAggregate : ChangeableAggregate
        {
            private void Apply(int @event)
            {
                this.Change = @event;
            }

            private void Apply(int? @event)
            {
                this.Change = @event;
            }
        }

        private class SomethingHappened
        {
        }

        private class SomethingElseHappened
        {
        }

        private class SomethingWierdHappened : SomethingHappened
        {
        }

        private class SomethingNeverHappened
        {
        }
    }
}
