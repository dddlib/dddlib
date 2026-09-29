using dddlib.Runtime;
using dddlib.Sdk;

namespace dddlib.Tests.Unit;

public class DefaultValueObjectSerializerTests
{
    [Test]
    public async Task CanSerializeValueObjectWithPropertySetter()
    {
        var serializer = new DefaultValueObjectSerializer<ValueObjectWithPropertySetter>();
        var valueObject = new ValueObjectWithPropertySetter { Property = "value" };

        var serializedValue = serializer.Serialize(valueObject);
        var valueObjectCopy = serializer.Deserialize(serializedValue);

        await Assert.That(valueObjectCopy).IsEqualTo(valueObject);
    }

    [Test]
    public async Task CanSerializeValueObjectWithConstructor()
    {
        var serializer = new DefaultValueObjectSerializer<ValueObjectWithConstructor>();
        var valueObject = new ValueObjectWithConstructor("value", 42);

        var serializedValue = serializer.Serialize(valueObject);
        var valueObjectCopy = serializer.Deserialize(serializedValue);

        await Assert.That(valueObjectCopy).IsEqualTo(valueObject);
    }

    [Test]
    public async Task CanSerializeValueObjectWithDateTime()
    {
        var serializer = new DefaultValueObjectSerializer<ValueObjectWithDateTime>();
        var valueObject = new ValueObjectWithDateTime(new DateTime(2017, 3, 8, 12, 34, 56, 789, DateTimeKind.Utc));

        var serializedValue = serializer.Serialize(valueObject);
        var valueObjectCopy = (ValueObjectWithDateTime)serializer.Deserialize(serializedValue);

        await Assert.That(serializedValue).Contains("2017-03-08T12:34:56.789Z");
        await Assert.That(valueObjectCopy).IsEqualTo(valueObject);
        await Assert.That(valueObjectCopy.Timestamp.Kind).IsEqualTo(DateTimeKind.Utc);
    }

    [Test]
    public async Task CannotDeserializeInvalidValueObject()
    {
        var serializer = new DefaultValueObjectSerializer<ValueObjectWithConstructor>();

        var action = () => serializer.Deserialize("not json");

        await Assert.That(action).Throws<RuntimeException>();
    }

    public class ValueObjectWithPropertySetter : ValueObject<ValueObjectWithPropertySetter>
    {
        public string? Property { get; set; }
    }

    public class ValueObjectWithConstructor : ValueObject<ValueObjectWithConstructor>
    {
        public ValueObjectWithConstructor(string text, int number)
        {
            this.Text = text;
            this.Number = number;
        }

        public string Text { get; }

        public int Number { get; }
    }

    public class ValueObjectWithDateTime : ValueObject<ValueObjectWithDateTime>
    {
        public ValueObjectWithDateTime(DateTime timestamp)
        {
            this.Timestamp = timestamp;
        }

        public DateTime Timestamp { get; }
    }
}
