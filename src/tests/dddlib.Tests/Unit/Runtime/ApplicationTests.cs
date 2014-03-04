// <copyright file="ApplicationTests.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Unit.Runtime
{
    using System;
    using dddlib.Runtime;
    using dddlib.Runtime.Configuration;
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
        public void ApplicationCanCreateTypeDescriptorForValidRuntimeType()
        {
            // arrange
            var type = typeof(Aggregate);

            var typeConfiguration = new TypeConfiguration();
            var typeConfigurationProvider = A.Fake<ITypeConfigurationProvider>(o => o.Strict());
            A.CallTo(() => typeConfigurationProvider.GetConfiguration(type)).Returns(typeConfiguration);
            
            var typeDescriptor = new TypeDescriptor();
            var typeAnalyzer = A.Fake<ITypeAnalyzer>(o => o.Strict());
            A.CallTo(() => typeAnalyzer.GetDescriptor(type, typeConfiguration)).Returns(typeDescriptor);

            using (new Application(typeConfigurationProvider, typeAnalyzer))
            {
                // act
                var actualTypeDescriptor = Application.Current.GetTypeDescriptor(type);

                // assert
                actualTypeDescriptor.Should().Be(typeDescriptor);
            }
        }

        [Fact]
        public void ApplicationCannotCreateTypeDescriptorForInvalidRuntimeType()
        {
            // arrange
            var type = typeof(object);
            var typeConfigurationProvider = A.Fake<ITypeConfigurationProvider>(o => o.Strict());

            using (new Application(typeConfigurationProvider))
            {
                // act
                Action action = () => Application.Current.GetTypeDescriptor(type);

                // assert
                action.ShouldThrow<RuntimeException>().And.Message.Should().Contain(type.FullName);
            }
        }

        [Fact]
        public void ApplicationThrowsRuntimeExceptionOnTypeConfigurationProviderException()
        {
            // arrange
            var innerException = new Exception();
            var typeConfigurationProvider = A.Fake<ITypeConfigurationProvider>(o => o.Strict());
            A.CallTo(() => typeConfigurationProvider.GetConfiguration(A<Type>.Ignored)).Throws(innerException);

            using (new Application(typeConfigurationProvider))
            {
                // act
                Action action = () => Application.Current.GetTypeDescriptor(typeof(Aggregate));

                // assert
                action.ShouldThrow<RuntimeException>().And.InnerException.Should().Be(innerException);
            }
        }

        [Fact]
        public void ApplicationThrowsRuntimeExceptionOnTypeConfigurationProviderRuntimeException()
        {
            // arrange
            var runtimeException = new RuntimeException();
            var typeConfigurationProvider = A.Fake<ITypeConfigurationProvider>(o => o.Strict());
            A.CallTo(() => typeConfigurationProvider.GetConfiguration(A<Type>.Ignored)).Throws(runtimeException);

            using (new Application(typeConfigurationProvider))
            {
                // act
                Action action = () => Application.Current.GetTypeDescriptor(typeof(Aggregate));

                // assert
                action.ShouldThrow<RuntimeException>().And.Should().Be(runtimeException);
            }
        }

        [Fact]
        public void ApplicationThrowsRuntimeExceptionOnTypeAnalyzerException()
        {
            // arrange
            var innerException = new Exception();
            var typeAnalyzer = A.Fake<ITypeAnalyzer>(o => o.Strict());
            A.CallTo(() => typeAnalyzer.GetDescriptor(A<Type>.Ignored, A<TypeConfiguration>.Ignored)).Throws(innerException);

            using (new Application(A.Fake<ITypeConfigurationProvider>(), typeAnalyzer))
            {
                // act
                Action action = () => Application.Current.GetTypeDescriptor(typeof(Aggregate));

                // assert
                action.ShouldThrow<RuntimeException>().And.InnerException.Should().Be(innerException);
            }
        }

        [Fact]
        public void ApplicationThrowsRuntimeExceptionOnTypeAnalyzerRuntimeException()
        {
            // arrange
            var runtimeException = new RuntimeException();
            var typeAnalyzer = A.Fake<ITypeAnalyzer>(o => o.Strict());
            A.CallTo(() => typeAnalyzer.GetDescriptor(A<Type>.Ignored, A<TypeConfiguration>.Ignored)).Throws(runtimeException);

            using (new Application(A.Fake<ITypeConfigurationProvider>(), typeAnalyzer))
            {
                // act
                Action action = () => Application.Current.GetTypeDescriptor(typeof(Aggregate));

                // assert
                action.ShouldThrow<RuntimeException>().And.Should().Be(runtimeException);
            }
        }

        private class Aggregate : AggregateRoot
        {
        }
    }
}
