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

            var expectedType = new AggregateRootType();
            var factory = A.Fake<ITypeFactory<AggregateRootType>>(o => o.Strict());
            A.CallTo(() => factory.Create(type)).Returns(expectedType);

            using (new Application(factory, A.Fake<ITypeFactory<EntityType>>(o => o.Strict()), A.Fake<ITypeFactory<ValueObjectType>>(o => o.Strict()), new Mapper()))
            {
                // act
                var actualType = Application.Current.GetAggregateRootType(type);

                // assert
                actualType.Should().Be(expectedType);
            }
        }

        [Fact]
        public void ApplicationCannotCreateRuntimeTypeForInvalidType()
        {
            // arrange
            var type = typeof(object);

            var expectedType = new AggregateRootType();
            var factory = A.Fake<ITypeFactory<AggregateRootType>>(o => o.Strict());
            A.CallTo(() => factory.Create(type)).Returns(expectedType);

            using (new Application(factory, A.Fake<ITypeFactory<EntityType>>(o => o.Strict()), A.Fake<ITypeFactory<ValueObjectType>>(o => o.Strict()), new Mapper()))
            {
                // act
                Action action = () => Application.Current.GetAggregateRootType(type);

                // assert
                action.ShouldThrow<RuntimeException>().And.Message.Should().Contain(type.FullName);
            }
        }

        [Fact]
        public void ApplicationThrowsRuntimeExceptionOnFactoryException()
        {
            // arrange
            var innerException = new Exception();
            var factory = A.Fake<ITypeFactory<AggregateRootType>>(o => o.Strict());
            A.CallTo(() => factory.Create(A<Type>.Ignored)).Throws(innerException);

            using (new Application(factory, A.Fake<ITypeFactory<EntityType>>(o => o.Strict()), A.Fake<ITypeFactory<ValueObjectType>>(o => o.Strict()), new Mapper()))
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
            var factory = A.Fake<ITypeFactory<AggregateRootType>>(o => o.Strict());
            A.CallTo(() => factory.Create(A<Type>.Ignored)).Throws(runtimeException);

            using (new Application(factory, A.Fake<ITypeFactory<EntityType>>(o => o.Strict()), A.Fake<ITypeFactory<ValueObjectType>>(o => o.Strict()), new Mapper()))
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
