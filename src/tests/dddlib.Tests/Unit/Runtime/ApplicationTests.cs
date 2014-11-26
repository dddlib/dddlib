// <copyright file="ApplicationTests.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Unit.Runtime
{
    using System;
    using dddlib.Runtime;
    using FakeItEasy;
    using FluentAssertions;
    using Xunit;

    /*  TODO (Cameron):
        Test for ObjectDisposedException
        Test caching of type descriptors  */

    public class ApplicationTests
    {
        [Fact]
        public void CanCreateApplication()
        {
            // arrange
            var defaultApplication = Application.Current;
            using (new Application())
            {
                // act
                var currentApplication = Application.Current;

                // assert
                currentApplication.Should().NotBe(defaultApplication);
            }
        }

        [Fact]
        public void CanDisposeApplication()
        {
            // arrange
            var defaultApplication = Application.Current;
            using (new Application())
            {
            }

            // act
            var currentApplication = Application.Current;

            // assert
            currentApplication.Should().Be(defaultApplication);
        }

        [Fact]
        public void CanDisposeApplicationMultipleTimes()
        {
            // arrange
            var application = new Application();
            
            // act
            application.Dispose();
            Action action = () => application.Dispose();

            // assert
            action.ShouldNotThrow();
        }

        [Fact]
        public void CanNestApplication()
        {
            // arrange
            var defaultApplication = Application.Current;
            using (new Application())
            {
                var firstApplication = Application.Current;
                using (new Application())
                {
                    var secondApplication = Application.Current;

                    // assert
                    firstApplication.Should().NotBe(defaultApplication);
                    secondApplication.Should().NotBe(defaultApplication);
                    firstApplication.Should().NotBe(secondApplication);
                }
            }
        }

        [Fact]
        public void CannotDisposeDefaultApplication()
        {
            // arrange
            var defaultApplication = Application.Current;

            // act
            ((IDisposable)defaultApplication).Dispose();

            // assert
            defaultApplication.Should().Be(Application.Current);
        }

        [Fact]
        public void ApplicationCanCreateRuntimeTypeForValidType()
        {
            // arrange
            var type = typeof(Aggregate);

            var expectedType = new AggregateRootType(type, null, null);
            var factory = A.Fake<Func<Type, AggregateRootType>>(o => o.Strict());
            A.CallTo(() => factory.Invoke(type)).Returns(expectedType);

            using (new Application(factory, t => null, t => null, new Mapper()))
            {
                // act
                var actualType = Application.Current.GetAggregateRootType(type);

                // assert
                actualType.Should().Be(expectedType);
            }
        }

        [Fact]
        public void ApplicationThrowsRuntimeExceptionOnFactoryException()
        {
            // arrange
            var innerException = new Exception();
            var factory = A.Fake<Func<Type, AggregateRootType>>(o => o.Strict());
            A.CallTo(() => factory.Invoke(A<Type>.Ignored)).Throws(innerException);

            using (new Application(factory, t => null, t => null, new Mapper()))
            {
                // act
                Action action = () => Application.Current.GetAggregateRootType(typeof(Aggregate));

                // assert
                action.ShouldThrow<RuntimeException>().And.InnerException.Should().Be(innerException);
            }
        }

        [Fact]
        public void ApplicationThrowsRuntimeExceptionOnFactoryRuntimeException()
        {
            // arrange
            var runtimeException = new RuntimeException();
            var factory = A.Fake<Func<Type, AggregateRootType>>(o => o.Strict());
            A.CallTo(() => factory.Invoke(A<Type>.Ignored)).Throws(runtimeException);

            using (new Application(factory, t => null, t => null, new Mapper()))
            {
                // act
                Action action = () => Application.Current.GetAggregateRootType(typeof(Aggregate));

                // assert
                action.ShouldThrow<RuntimeException>().And.Should().Be(runtimeException);
            }
        }

        private class Aggregate : AggregateRoot
        {
        }
    }
}
