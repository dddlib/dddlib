using dddlib.Persistence.Sdk;

namespace dddlib.Persistence.Tests.Unit;

public class JsonSerializerTests
{
    [Test]
    public async Task TestString()
    {
        var serializer = new DefaultNaturalKeySerializer();
        var naturalKey = "Key";

        var serializedNaturalKey = serializer.Serialize(typeof(string), naturalKey);
        var deserializedNaturalKey = serializer.Deserialize(typeof(string), serializedNaturalKey);

        await Assert.That(deserializedNaturalKey).IsEqualTo(naturalKey);
    }

    [Test]
    public async Task TestGuid()
    {
        var serializer = new DefaultNaturalKeySerializer();
        var naturalKey = Guid.NewGuid();

        var serializedNaturalKey = serializer.Serialize(typeof(Guid), naturalKey);
        var deserializedNaturalKey = serializer.Deserialize(typeof(Guid), serializedNaturalKey);

        await Assert.That(deserializedNaturalKey).IsEqualTo(naturalKey);
    }

    [Test]
    public async Task TestSillyValueObject()
    {
        var serializer = new DefaultNaturalKeySerializer();
        var naturalKey = new SillyValueObject { Value = "Key" };

        var serializedNaturalKey = serializer.Serialize(typeof(SillyValueObject), naturalKey);
        var deserializedNaturalKey = serializer.Deserialize(typeof(SillyValueObject), serializedNaturalKey);

        await Assert.That(deserializedNaturalKey).IsEqualTo(naturalKey);
    }

    [Test]
    public async Task TestSensibleValueObject()
    {
        var serializer = new DefaultNaturalKeySerializer();
        var naturalKey = new SensibleValueObject("Key");

        var serializedNaturalKey = serializer.Serialize(typeof(SensibleValueObject), naturalKey);
        var deserializedNaturalKey = serializer.Deserialize(typeof(SensibleValueObject), serializedNaturalKey);

        await Assert.That(deserializedNaturalKey).IsEqualTo(naturalKey);
    }

    private sealed class SillyValueObject : ValueObject<SillyValueObject>
    {
        public string? Value { get; set; }
    }

    private sealed class SensibleValueObject : ValueObject<SensibleValueObject>
    {
        public SensibleValueObject(string value)
        {
            ArgumentNullException.ThrowIfNull(value);

            this.Value = value;
        }

        public string Value { get; }
    }
}
