using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace dddlib.Generators.Tests;

public class SymbolExtensionsTests
{
    [Test]
    [Arguments("String", true)]
    [Arguments("NullableString", true)]
    [Arguments("Integer", true)]
    [Arguments("NullableInteger", true)]
    [Arguments("Enumeration", true)]
    [Arguments("Struct", true)]
    [Arguments("ValueObject", true)]
    [Arguments("Record", true)]
    [Arguments("OverridesEquals", true)]
    [Arguments("InheritsEqualsOverride", true)]
    [Arguments("Equatable", true)]
    [Arguments("PlainClass", false)]
    [Arguments("EquatableToSomethingElse", false)]
    [Arguments("Array", false)]
    [Arguments("Object", false)]
    public async Task HasValueEquality(string property, bool expected)
    {
        var compilation = Compile("""
            using System;
            using dddlib;

            public enum Colour { Red }

            public struct Point { public int X; }

            public class Money : ValueObject<Money> { public decimal Amount { get; set; } }

            public record Name(string Value);

            public class Overriding
            {
                public override bool Equals(object? obj) => true;
                public override int GetHashCode() => 0;
            }

            public class Inheriting : Overriding { }

            public class Equatable : IEquatable<Equatable>
            {
                public bool Equals(Equatable? other) => true;
            }

            public class Mismatched : IEquatable<string>
            {
                public bool Equals(string? other) => true;
            }

            public class Plain { }

            public class Holder
            {
                public string String { get; set; } = string.Empty;
                public string? NullableString { get; set; }
                public int Integer { get; set; }
                public int? NullableInteger { get; set; }
                public Colour Enumeration { get; set; }
                public Point Struct { get; set; }
                public Money? ValueObject { get; set; }
                public Name? Record { get; set; }
                public Overriding? OverridesEquals { get; set; }
                public Inheriting? InheritsEqualsOverride { get; set; }
                public Equatable? Equatable { get; set; }
                public Plain? PlainClass { get; set; }
                public Mismatched? EquatableToSomethingElse { get; set; }
                public int[]? Array { get; set; }
                public object? Object { get; set; }
            }
            """);

        var type = compilation.GetTypeByMetadataName("Holder")!.GetMembers(property).OfType<IPropertySymbol>().Single().Type;

        await Assert.That(type.HasValueEquality()).IsEqualTo(expected);
    }

    [Test]
    [Arguments("SettableProperties")]
    [Arguments("InitProperties")]
    [Arguments("ConstructorMatchingProperties")]
    [Arguments("ConstructorMatchingPropertiesByCase")]
    [Arguments("PositionalRecord")]
    [Arguments("ComputedProperty")]
    [Arguments("IgnoredProperty")]
    [Arguments("IncludedPrivateSetter")]
    [Arguments("AnnotatedConstructor")]
    [Arguments("ConstructorAndSettableProperty")]
    [Arguments("InheritedSettableProperty")]
    public async Task IsDefaultSerializable(string type)
    {
        var result = Serializability(type);

        await Assert.That(result.IsSerializable).IsTrue();
        await Assert.That(result.UnloadedProperties).IsEmpty();
        await Assert.That(result.UnboundParameter).IsNull();
    }

    [Test]
    [Arguments("GetOnlyProperty", "Value")]
    [Arguments("PrivateSetter", "Value")]
    [Arguments("ConstructorMissingAProperty", "Other")]
    [Arguments("InheritedGetOnlyProperty", "Value")]
    public async Task IsNotDefaultSerializableWhenAPropertyIsNotLoaded(string type, string property)
    {
        var result = Serializability(type);

        await Assert.That(result.IsSerializable).IsFalse();
        await Assert.That(result.UnloadedProperties.Select(static unloaded => unloaded.Name)).IsEquivalentTo([property]);
    }

    [Test]
    public async Task NamesEveryPropertyThatIsNotLoaded()
    {
        var result = Serializability("SeveralGetOnlyProperties");

        await Assert.That(result.UnloadedProperties.Select(static unloaded => unloaded.Name)).IsEquivalentTo(["First", "Second"]);
    }

    [Test]
    public async Task IsNotDefaultSerializableWhenAConstructorParameterMatchesNoProperty()
    {
        var result = Serializability("ConstructorWithAnUnboundParameter");

        await Assert.That(result.IsSerializable).IsFalse();
        await Assert.That(result.UnboundParameter?.Name).IsEqualTo("other");
        await Assert.That(result.UnloadedProperties).IsEmpty();
    }

    [Test]
    [Arguments("SeveralConstructors")]
    [Arguments("NoPublicConstructor")]
    [Arguments("Abstract")]
    public async Task IsNotDefaultSerializableWithoutAUsableConstructor(string type)
    {
        var result = Serializability(type);

        await Assert.That(result.IsSerializable).IsFalse();
        await Assert.That(result.UnloadedProperties).IsEmpty();
        await Assert.That(result.UnboundParameter).IsNull();
    }

    private static DefaultSerializability Serializability(string type) =>
        Compile("""
            using System.Text.Json.Serialization;

            public class SettableProperties
            {
                public string? Value { get; set; }
            }

            public class InitProperties
            {
                public string? Value { get; init; }
            }

            public class ConstructorMatchingProperties
            {
                public ConstructorMatchingProperties(string value, int other) { this.Value = value; this.Other = other; }
                public string Value { get; }
                public int Other { get; }
            }

            public class ConstructorMatchingPropertiesByCase
            {
                public ConstructorMatchingPropertiesByCase(string VALUE) { this.Value = VALUE; }
                public string Value { get; }
            }

            public record PositionalRecord(string Value, int Other);

            public class ComputedProperty
            {
                public string? Value { get; set; }
                public int Length => this.Value?.Length ?? 0;
            }

            public class IgnoredProperty
            {
                public string? Value { get; set; }
                [JsonIgnore] public string Ignored { get; } = string.Empty;
            }

            public class IncludedPrivateSetter
            {
                [JsonInclude] public string? Value { get; private set; }
            }

            public class AnnotatedConstructor
            {
                public AnnotatedConstructor() { this.Value = string.Empty; }
                [JsonConstructor] public AnnotatedConstructor(string value) { this.Value = value; }
                public string Value { get; }
            }

            public class ConstructorAndSettableProperty
            {
                public ConstructorAndSettableProperty(string value) { this.Value = value; }
                public string Value { get; }
                public int Other { get; set; }
            }

            public class InheritedSettableProperty : SettableProperties
            {
                public int Other { get; set; }
            }

            public class GetOnlyProperty
            {
                public string Value { get; } = string.Empty;
            }

            public class SeveralGetOnlyProperties
            {
                public string First { get; } = string.Empty;
                public string Second { get; } = string.Empty;
                public string? Third { get; set; }
            }

            public class PrivateSetter
            {
                public string? Value { get; private set; }
            }

            public class ConstructorMissingAProperty
            {
                public ConstructorMissingAProperty(string value) { this.Value = value; }
                public string Value { get; }
                public int Other { get; }
            }

            public class InheritedGetOnlyProperty : GetOnlyProperty
            {
                public int Other { get; set; }
            }

            public class ConstructorWithAnUnboundParameter
            {
                public ConstructorWithAnUnboundParameter(string value, int other) { this.Value = value; }
                public string Value { get; }
            }

            public class SeveralConstructors
            {
                public SeveralConstructors(string value) { this.Value = value; }
                public SeveralConstructors(string value, int other) { this.Value = value; }
                public string Value { get; }
            }

            public class NoPublicConstructor
            {
                private NoPublicConstructor() { }
                public string? Value { get; set; }
            }

            public abstract class Abstract
            {
                public string? Value { get; set; }
            }
            """).GetTypeByMetadataName(type)!.IsDefaultSerializable();

    private static CSharpCompilation Compile(string source)
    {
        var compilation = TestCompilation.Create(source);

        var errors = compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToList();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        return compilation;
    }
}
