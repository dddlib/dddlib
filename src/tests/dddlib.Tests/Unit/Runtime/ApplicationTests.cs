// <copyright file="ApplicationTests.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Unit.Runtime
{
    using System;
    using dddlib.Runtime;
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
    }
}
