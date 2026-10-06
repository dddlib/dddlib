using dddlib.Configuration;
using dddlib.Persistence.Sdk;
using dddlib.Tests.Support;

namespace dddlib.Persistence.Tests.Unit;

// A natural key is compared as text by the repository only when equal keys always serialize to the same text.
public class NaturalKeySerializerTests : Feature
{
    public enum Colour
    {
        Red,
    }

    [Test]
    [Arguments(typeof(string))]
    [Arguments(typeof(int))]
    [Arguments(typeof(long))]
    [Arguments(typeof(Guid))]
    [Arguments(typeof(bool))]
    [Arguments(typeof(char))]
    [Arguments(typeof(Colour))]
    [Arguments(typeof(int?))]
    [Arguments(typeof(Code))]
    [Arguments(typeof(Compound))]
    public async Task EqualKeysOfTheTypeSerializeAlike(Type naturalKeyType)
    {
        await Assert.That(new DefaultNaturalKeySerializer().IsCanonical(naturalKeyType)).IsTrue();
    }

    [Test]
    [Arguments(typeof(decimal))] // 1.0m equals 1.00m
    [Arguments(typeof(double))] // 0.0 equals -0.0
    [Arguments(typeof(DateTime))] // equality ignores the kind, which is serialized
    [Arguments(typeof(DateTimeOffset))] // equality ignores the offset, which is serialized
    [Arguments(typeof(object))]
    [Arguments(typeof(string[]))]
    [Arguments(typeof(CaseInsensitiveCode))]
    [Arguments(typeof(CustomSerializedCode))]
    [Arguments(typeof(UnsealedCode))]
    [Arguments(typeof(Amount))]
    [Arguments(typeof(Wrapper))]
    public async Task EqualKeysOfTheTypeCanSerializeDifferently(Type naturalKeyType)
    {
        await Assert.That(new DefaultNaturalKeySerializer().IsCanonical(naturalKeyType)).IsFalse();
    }

    public sealed class Code(string value) : ValueObject<Code>
    {
        public string Value { get; } = value;
    }

    public sealed class Compound(Code code, int number) : ValueObject<Compound>
    {
        public Code Code { get; } = code;

        public int Number { get; } = number;
    }

    public sealed class CaseInsensitiveCode(string value) : ValueObject<CaseInsensitiveCode>
    {
        public string Value { get; } = value;
    }

    public sealed class CustomSerializedCode(string value) : ValueObject<CustomSerializedCode>
    {
        public string Value { get; } = value;
    }

    public class UnsealedCode(string value) : ValueObject<UnsealedCode>
    {
        public string Value { get; } = value;
    }

    public sealed class Amount(decimal value) : ValueObject<Amount>
    {
        public decimal Value { get; } = value;
    }

    // equal only if the nested value object is, which compares case-insensitively
    public sealed class Wrapper(CaseInsensitiveCode code) : ValueObject<Wrapper>
    {
        public CaseInsensitiveCode Code { get; } = code;
    }

    private sealed class CaseInsensitiveCodeBootstrapper : IBootstrap<CaseInsensitiveCode>
    {
        public void Bootstrap(IConfiguration configure) =>
            configure.ValueObject<CaseInsensitiveCode>().ToUseEqualityComparer(new CaseInsensitiveComparer());
    }

    private sealed class CustomSerializedCodeBootstrapper : IBootstrap<CustomSerializedCode>
    {
        public void Bootstrap(IConfiguration configure) =>
            configure.ValueObject<CustomSerializedCode>().ToUseValueObjectSerializer(code => code.Value, value => new CustomSerializedCode(value));
    }

    private sealed class CaseInsensitiveComparer : IEqualityComparer<CaseInsensitiveCode>
    {
        public bool Equals(CaseInsensitiveCode? x, CaseInsensitiveCode? y) => string.Equals(x?.Value, y?.Value, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(CaseInsensitiveCode obj) => StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Value);
    }
}
