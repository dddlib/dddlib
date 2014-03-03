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

        [Fact(Skip = "Incomplete")]
        public void CanCreateTypeDescriptorForValidRuntimeType()
        {
            // arrange
            var type = typeof(object);
            var typeConfigurationProvider = A.Fake<ITypeConfigurationProvider>(o => o.Strict());

            using (new Application(typeConfigurationProvider))
            {
                // act
                var typeDescriptor = Application.Current.GetTypeDescriptor(type);

                // assert
            }
        }

        [Fact]
        public void CannotCreateTypeDescriptorForInvalidRuntimeType()
        {
            // arrange
            var type = typeof(object);
            var typeConfigurationProvider = A.Fake<ITypeConfigurationProvider>(o => o.Strict());

            using (new Application(typeConfigurationProvider))
            {
                // act
                Action action = () => Application.Current.GetTypeDescriptor(type);

                // assert
                action.ShouldThrow<RuntimeException>();
            }
        }

        private class Aggregate : AggregateRoot
        {
        }
    }
}
