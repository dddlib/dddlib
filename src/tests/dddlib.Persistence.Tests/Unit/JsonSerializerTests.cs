// <copyright file="JsonSerializerTests.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Tests.Unit
{
    using System;
    using dddlib.Persistence.Sdk;
    using Xunit;

    public class JsonSerializerTests
    {
        [Fact]
        public void TestString()
        {
            // arrange
            var serializer = new DefaultNaturalKeySerializer();
            var naturalKey = "Key";

            // act
            var serializedNaturalKey = serializer.Serialize(typeof(string), naturalKey);
            var deserializedNaturalKey = serializer.Deserialize(typeof(string), serializedNaturalKey);

            // assert
            Assert.Equal(naturalKey, deserializedNaturalKey);
        }

        [Fact]
        public void TestGuid()
        {
            // arrange
            var serializer = new DefaultNaturalKeySerializer();
            var naturalKey = Guid.NewGuid();

            // act
            var serializedNaturalKey = serializer.Serialize(typeof(Guid), naturalKey);
            var deserializedNaturalKey = serializer.Deserialize(typeof(Guid), serializedNaturalKey);

            // assert
            Assert.Equal(naturalKey, deserializedNaturalKey);
        }

        [Fact]
        public void TestSillyValueObject()
        {
            // arrange
            var serializer = new DefaultNaturalKeySerializer();
            var naturalKey = new SillyValueObject { Value = "Key" };

            // act
            var serializedNaturalKey = serializer.Serialize(typeof(SillyValueObject), naturalKey);
            var deserializedNaturalKey = serializer.Deserialize(typeof(SillyValueObject), serializedNaturalKey);

            // assert
            Assert.Equal(naturalKey, deserializedNaturalKey);
        }

        [Fact]
        public void TestSensibleValueObject()
        {
            // arrange
            var serializer = new DefaultNaturalKeySerializer();
            var naturalKey = new SensibleValueObject("Key");

            // act
            var serializedNaturalKey = serializer.Serialize(typeof(SensibleValueObject), naturalKey);
            var deserializedNaturalKey = serializer.Deserialize(typeof(SensibleValueObject), serializedNaturalKey);

            // assert
            Assert.Equal(naturalKey, deserializedNaturalKey);
        }

        private class SillyValueObject : ValueObject<SillyValueObject>
        {
            public string Value { get; set; }
        }

        private class SensibleValueObject : ValueObject<SensibleValueObject>
        {
            public SensibleValueObject(string value)
            {
                Guard.Against.Null(() => value);

                this.Value = value;
            }

            public string Value { get; private set; }
        }
    }
}
