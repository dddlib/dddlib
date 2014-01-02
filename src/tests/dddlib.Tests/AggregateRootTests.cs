// <copyright file="AggregateRootTests.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.Serialization;
    using dddlib.Tests.Support.Events;
    using dddlib.Tests.Support.Model;
    using FluentAssertions;
    using Xunit;

    [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1124:DoNotUseRegions", Justification = "It's OK here.")]
    public class AggregateRootTests
    {
        [Fact]
        public void CanInstantiateEmptyAggregate()
        {
            // act
            new EmptyAggregate();
        }

        #region Handler Apply Method(s)

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

        #endregion

        #region Check Instantiation

        //// TODO: reconstitute
        //// check apply still works
        //// check read only private member variable instantiation
        //// check base constructor instantiation

        #endregion

        [Fact]
        public void CanEndLifecycle()
        {
            // arrange
            var aggregate = new LifecycleAggregate();

            // act
            aggregate.DoSomething(); // NOTE (Cameron): Proves we can do before we destroy.
            aggregate.Destroy();
            Action action = () => aggregate.DoSomething();

            // assert
            action.ShouldThrow<BusinessException>();
        }

        [Fact]
        public void CanReconstitute()
        {
            // arrange
            var memento = (object)null;
            var events = new[] { new SomethingHappened() };
            
            // act
            var aggregate = Reconstitute<PersistedAggregate>(memento, events);
            aggregate.MakeSomethingHappen();

            // assert
            aggregate.ThingsThatHappened.Should().HaveCount(2);
        }

        private static T Reconstitute<T>(object memento, IEnumerable<object> events)
            where T : AggregateRoot
        {
            var aggregate = Activator.CreateInstance(typeof(T), true) as IAggregateRoot;
            aggregate.Initialize(memento, events, "state");
            return (T)aggregate;
        }
    }
}
