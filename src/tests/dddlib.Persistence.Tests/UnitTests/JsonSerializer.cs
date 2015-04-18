// <copyright file="JsonSerializer.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Tests.UnitTests
{
    using System;
    using dddlib.Persistence.Sdk;
    using Xunit;

    public class JsonSerializer
    {
        [Fact]
        public void TestString()
        {
            // arrange
            var serializer = new DefaultNaturalKeySerializer();
            var naturalKey = "Key";

            // act
            var serializedNaturalKey = serializer.Serialize(naturalKey);
            var deserializedNaturalKey = serializer.Deserialize<string>(serializedNaturalKey);

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
            var serializedNaturalKey = serializer.Serialize(naturalKey);
            var deserializedNaturalKey = serializer.Deserialize<Guid>(serializedNaturalKey);

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
            var serializedNaturalKey = serializer.Serialize(naturalKey);
            var deserializedNaturalKey = serializer.Deserialize<SillyValueObject>(serializedNaturalKey);

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
            var serializedNaturalKey = serializer.Serialize(naturalKey);
            var deserializedNaturalKey = serializer.Deserialize<SensibleValueObject>(serializedNaturalKey);

            // assert
            Assert.Equal(naturalKey, deserializedNaturalKey);
        }

        private class SillyValueObject : ValueObject<SillyValueObject>
        {
            public string Value { get; set; }
        }

        private class SensibleValueObject : ValueObject<SensibleValueObject>
        {
            public SensibleValueObject(string key)
            {
                Guard.Against.Null(() => key);

                this.Value = key;
            }

            public string Value { get; private set; }
        }
    }
}
