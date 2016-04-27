// <copyright file="DefaultValueObjectSerializerTests.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Unit
{
    using dddlib.Sdk;
    using Xunit;

    public class DefaultValueObjectSerializerTests
    {
        [Fact]
        public void CanSerializeValueObjectWithPropertySetter()
        {
            var serializer = new DefaultValueObjectSerializer<ValueObjectWithPropertySetter>();
            var valueObject = new ValueObjectWithPropertySetter { Property = "value" };

            var serializedValue = serializer.Serialize(valueObject);
            var valueObjectCopy = serializer.Deserialize(serializedValue);

            Assert.Equal(valueObject, valueObjectCopy);
        }

        [Fact]
        public void CanSerializeValueObjectWithConstructor()
        {
            var serializer = new DefaultValueObjectSerializer<ValueObjectWithConstructor>();
            var valueObject = new ValueObjectWithConstructor("value", 42);

            var serializedValue = serializer.Serialize(valueObject);
            var valueObjectCopy = serializer.Deserialize(serializedValue);

            Assert.Equal(valueObject, valueObjectCopy);
        }

        public class ValueObjectWithPropertySetter : ValueObject<ValueObjectWithPropertySetter>
        {
            public string Property { get; set; }
        }

        public class ValueObjectWithConstructor : ValueObject<ValueObjectWithConstructor>
        {
            public ValueObjectWithConstructor(string @string, int @int)
            {
                this.String = @string;
                this.Int = @int;
            }

            public string String { get; private set; }

            public int Int { get; private set; }
        }
    }
}
