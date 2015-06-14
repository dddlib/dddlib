// <copyright file="SqlServerIdentityMapTests.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Tests.Integration
{
    using System;
    using dddlib.Persistence.SqlServer;
    using dddlib.Persistence.Tests.Sdk;
    using FluentAssertions;
    using Xunit;

    public class SqlServerIdentityMapTests : Integration.Database
    {
        public SqlServerIdentityMapTests(SqlServerFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public void TryGetWhenIdentityMapDoesNotContainNaturalKey()
        {
            // arrange
            var identityMap = new SqlServerIdentityMap(this.ConnectionString);
            var registration = new Registration(Guid.NewGuid().ToString("N"));
            var identity = default(Guid);

            // act
            var success = identityMap.TryGet(typeof(Car), typeof(Registration), registration, out identity);

            // assert
            success.Should().BeFalse();
        }

        [Fact]
        public void TryGetWhenIdentityMapDoesContainNaturalKey()
        {
            // arrange
            var identityMap = new SqlServerIdentityMap(this.ConnectionString);
            var otherIdentityMap = new SqlServerIdentityMap(this.ConnectionString);
            var registration = new Registration(Guid.NewGuid().ToString("N"));
            var actualIdentity = default(Guid);
            var sameIdentity = default(Guid);

            // act
            var expectedIdentity = identityMap.GetOrAdd(typeof(Car), typeof(Registration), registration);
            var success = otherIdentityMap.TryGet(typeof(Car), typeof(Registration), registration, out actualIdentity);
            var anotherSuccess = otherIdentityMap.TryGet(typeof(Car), typeof(Registration), registration, out sameIdentity);

            // assert
            success.Should().BeTrue();
            actualIdentity.Should().Be(expectedIdentity);
            anotherSuccess.Should().BeTrue();
            sameIdentity.Should().Be(expectedIdentity);
        }

        [Fact]
        public void GetOrAddWhenIdentityMapDoesNotContainNaturalKey()
        {
            // arrange
            var identityMap = new SqlServerIdentityMap(this.ConnectionString);
            var registration = new Registration(Guid.NewGuid().ToString("N"));

            // act
            var identity = identityMap.GetOrAdd(typeof(Car), typeof(Registration), registration);
            var sameIdentity = identityMap.GetOrAdd(typeof(Car), typeof(Registration), registration);

            // assert
            identity.Should().Be(sameIdentity);
        }

        [Fact]
        public void GetOrAddWhenIdentityMapDoesContainNaturalKey()
        {
            // arrange
            var identityMap = new SqlServerIdentityMap(this.ConnectionString);
            var otherIdentityMap = new SqlServerIdentityMap(this.ConnectionString);
            var registration = new Registration(Guid.NewGuid().ToString("N"));

            // act
            var expectedIdentity = identityMap.GetOrAdd(typeof(Car), typeof(Registration), registration);
            var actualIdentity = otherIdentityMap.GetOrAdd(typeof(Car), typeof(Registration), registration);

            // assert
            actualIdentity.Should().Be(expectedIdentity);
        }

        [Fact]
        public void UseAlternateSchema()
        {
            // arrange
            var identityMap = new SqlServerIdentityMap(this.ConnectionString, "alternate");
            var registration = new Registration(Guid.NewGuid().ToString("N"));
            var actualIdentity = default(Guid);

            // act
            var initialSuccess = identityMap.TryGet(typeof(Car), typeof(Registration), registration, out actualIdentity);
            var expectedIdentity = identityMap.GetOrAdd(typeof(Car), typeof(Registration), registration);
            var subsequentSuccess = identityMap.TryGet(typeof(Car), typeof(Registration), registration, out actualIdentity);

            // assert
            initialSuccess.Should().BeFalse();
            subsequentSuccess.Should().BeTrue();
            actualIdentity.Should().Be(expectedIdentity);
        }

        private class Registration : ValueObject<Registration>
        {
            public Registration(string number)
            {
                Guard.Against.Null(() => number);

                this.Number = number;
            }

            public string Number { get; private set; }
        }

        private class Car : AggregateRoot
        {
            public Registration Registration { get; private set; }
        }
    }
}
