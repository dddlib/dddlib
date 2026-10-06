using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;

namespace dddlib.Generators.Tests;

public class DomainTypeAnalyzerTests
{
    [Test]
    [Arguments("protected override object? GetState() => null;", "'GetState' but not 'SetState'", "loaded")]
    [Arguments("protected override void SetState(object memento) { }", "'SetState' but not 'GetState'", "saved")]
    public async Task ReportsAnAggregateRootThatOverridesOnlyOneMementoMethod(string member, string expected, string consequence)
    {
        var diagnostics = await AnalyzeAsync($$"""
            using dddlib;

            public partial class Subject : AggregateRoot
            {
                [NaturalKey] public string? Id { get; set; }

                {{member}}
            }
            """);

        await Assert.That(diagnostics).Count().IsEqualTo(1);
        await Assert.That(diagnostics[0].Id).IsEqualTo("DDDLIB013");
        await Assert.That(diagnostics[0].Severity).IsEqualTo(DiagnosticSeverity.Warning);
        await Assert.That(diagnostics[0].GetMessage(CultureInfo.InvariantCulture)).Contains(expected);
        await Assert.That(diagnostics[0].GetMessage(CultureInfo.InvariantCulture)).EndsWith(consequence);
    }

    [Test]
    public async Task DoesNotReportAnAggregateRootThatOverridesBothMementoMethodsOrNeither()
    {
        var diagnostics = await AnalyzeAsync("""
            using dddlib;

            public partial class Both : AggregateRoot
            {
                [NaturalKey] public string? Id { get; set; }

                protected override object? GetState() => null;

                protected override void SetState(object memento) { }
            }

            public partial class Neither : AggregateRoot
            {
                [NaturalKey] public string? Id { get; set; }
            }

            // the pair is completed by the subclass
            public abstract partial class Base : AggregateRoot
            {
                [NaturalKey] public string? Id { get; set; }

                protected override object? GetState() => null;
            }

            public partial class Derived : Base
            {
                protected override void SetState(object memento) { }
            }
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    [Arguments("")]
    [Arguments("internal sealed class Bootstrapper : IBootstrapper { public void Bootstrap(IConfiguration configure) { configure.AggregateRoot<Other>().ToReconstituteUsing(() => new Other()); } }")]
    public async Task ReportsAnAggregateRootThatCannotBeReconstituted(string bootstrapper)
    {
        var diagnostics = await AnalyzeAsync($$"""
            using dddlib;
            using dddlib.Configuration;

            public partial class Subject : AggregateRoot
            {
                public Subject(string id) { this.Id = id; }

                [NaturalKey] public string Id { get; }
            }

            public partial class Other : AggregateRoot
            {
                [NaturalKey] public string? Id { get; set; }
            }

            {{bootstrapper}}
            """);

        await Assert.That(diagnostics).Count().IsEqualTo(1);
        await Assert.That(diagnostics[0].Id).IsEqualTo("DDDLIB014");
        await Assert.That(diagnostics[0].Severity).IsEqualTo(DiagnosticSeverity.Warning);
        await Assert.That(diagnostics[0].GetMessage(CultureInfo.InvariantCulture)).Contains("'Subject'");
    }

    [Test]
    public async Task DoesNotReportAnAggregateRootThatCanBeReconstituted()
    {
        var diagnostics = await AnalyzeAsync("""
            using dddlib;
            using dddlib.Configuration;

            public partial class WithConstructor : AggregateRoot
            {
                public WithConstructor(string id) { this.Id = id; }

                protected WithConstructor() { this.Id = string.Empty; }

                [NaturalKey] public string Id { get; }
            }

            public partial class WithFactory : AggregateRoot
            {
                public WithFactory(string id) { this.Id = id; }

                [NaturalKey] public string Id { get; }
            }

            public abstract partial class Abstract : AggregateRoot
            {
                protected Abstract(string id) { this.Id = id; }

                [NaturalKey] public string Id { get; }
            }

            internal sealed class Bootstrapper : IBootstrapper
            {
                public void Bootstrap(IConfiguration configure)
                {
                    configure.AggregateRoot<WithFactory>().ToReconstituteUsing(() => new WithFactory(string.Empty));
                }
            }
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task ReportsAnAggregateRootWithoutANaturalKey()
    {
        var diagnostics = await AnalyzeAsync("""
            using dddlib;

            public partial class Subject : AggregateRoot
            {
                public string? Id { get; set; }
            }

            // an entity without a natural key is legal
            public partial class Line : Entity
            {
            }
            """);

        await Assert.That(diagnostics).Count().IsEqualTo(1);
        await Assert.That(diagnostics[0].Id).IsEqualTo("DDDLIB015");
        await Assert.That(diagnostics[0].Severity).IsEqualTo(DiagnosticSeverity.Warning);
        await Assert.That(diagnostics[0].GetMessage(CultureInfo.InvariantCulture)).Contains("'Subject'");
    }

    [Test]
    public async Task DoesNotReportAnAggregateRootWithANaturalKey()
    {
        var diagnostics = await AnalyzeAsync("""
            using dddlib;
            using dddlib.Configuration;

            public abstract partial class Base : AggregateRoot
            {
                [NaturalKey] public string? Id { get; set; }
            }

            public partial class Inheriting : Base
            {
            }

            public partial class Configured : AggregateRoot
            {
                public string? Id { get; set; }
            }

            public abstract partial class Abstract : AggregateRoot
            {
            }

            internal sealed class Bootstrapper : IBootstrapper
            {
                public void Bootstrap(IConfiguration configure)
                {
                    configure.AggregateRoot<Configured>().ToUseNaturalKey(configured => configured.Id);
                }
            }
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task DoesNotReportAgainstABootstrapperItCannotRead()
    {
        // OrderConfiguration stands for whatever a custom bootstrapper provider calls
        var diagnostics = await AnalyzeAsync("""
            using dddlib;
            using dddlib.Configuration;

            public partial class Subject : AggregateRoot
            {
                public Subject(string id) { this.Id = id; }

                public string Id { get; }
            }

            public partial class Opaque : ValueObject<Opaque>
            {
            }

            internal sealed class SubjectConfiguration
            {
                public void Bootstrap(IConfiguration configure)
                {
                }
            }
            """);

        await Assert.That(diagnostics.Select(static d => d.Id)).IsEquivalentTo(["DDDLIB004"]);
    }

    [Test]
    public async Task DoesNotReportAValueObjectWithoutPropertiesThatHasAConfiguredComparer()
    {
        var diagnostics = await AnalyzeAsync("""
            using System.Collections.Generic;
            using dddlib;
            using dddlib.Configuration;

            public partial class Opaque : ValueObject<Opaque>
            {
            }

            internal sealed class Bootstrapper : IBootstrapper
            {
                public void Bootstrap(IConfiguration configure)
                {
                    configure.ValueObject<Opaque>().ToUseEqualityComparer(EqualityComparer<Opaque>.Default);
                }
            }
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task ReportsNaturalKeyAttributesThatAreIgnored()
    {
        var diagnostics = await AnalyzeAsync("""
            using dddlib;

            public partial class Thing : Entity
            {
                [NaturalKey] internal string? Hidden { get; set; }
                [NaturalKey] public static string? Shared { get; set; }
                [NaturalKey] public string? WriteOnly { set { } }
                [NaturalKey] public string? this[int index] => null;
            }

            public class Plain
            {
                [NaturalKey] public string? Key { get; set; }
            }
            """);

        var messages = diagnostics.Where(static d => d.Id == "DDDLIB016").Select(static d => d.GetMessage(CultureInfo.InvariantCulture)).ToList();

        await Assert.That(diagnostics).Count().IsEqualTo(5);
        await Assert.That(diagnostics.All(static d => d.Severity == DiagnosticSeverity.Error)).IsTrue();
        await Assert.That(messages.Count(static m => m.Contains("Thing.Hidden", StringComparison.Ordinal) && m.EndsWith("it is not public", StringComparison.Ordinal))).IsEqualTo(1);
        await Assert.That(messages.Count(static m => m.Contains("Thing.Shared", StringComparison.Ordinal) && m.EndsWith("it is static", StringComparison.Ordinal))).IsEqualTo(1);
        await Assert.That(messages.Count(static m => m.Contains("Thing.WriteOnly", StringComparison.Ordinal) && m.EndsWith("it has no getter", StringComparison.Ordinal))).IsEqualTo(1);
        await Assert.That(messages.Count(static m => m.EndsWith("it is an indexer", StringComparison.Ordinal))).IsEqualTo(1);
        await Assert.That(messages.Count(static m => m.Contains("Plain.Key", StringComparison.Ordinal) && m.EndsWith("'Plain' is not an entity or an aggregate root", StringComparison.Ordinal))).IsEqualTo(1);
    }

    [Test]
    public async Task ReportsANaturalKeyThatDoesNotRoundTrip()
    {
        var diagnostics = await AnalyzeAsync("""
            using dddlib;
            using dddlib.Configuration;

            public partial class ByReference : AggregateRoot
            {
                [NaturalKey] public Code? Id { get; set; }
            }

            public partial class ByUnreadableValueObject : AggregateRoot
            {
                [NaturalKey] public Reference? Id { get; set; }
            }

            public partial class ByBootstrapper : AggregateRoot
            {
                public Code? Id { get; set; }
            }

            public class Code
            {
                public string? Value { get; set; }
            }

            public partial class Reference : ValueObject<Reference>
            {
                public Reference(string value, int checksum) { this.Value = value + checksum; }

                public string Value { get; }
            }

            internal sealed class Bootstrapper : IBootstrapper
            {
                public void Bootstrap(IConfiguration configure)
                {
                    configure.AggregateRoot<ByBootstrapper>().ToUseNaturalKey(subject => subject.Id);
                }
            }
            """);

        var messages = diagnostics.Where(static d => d.Id == "DDDLIB017").Select(static d => d.GetMessage(CultureInfo.InvariantCulture)).ToList();

        await Assert.That(diagnostics).Count().IsEqualTo(3);
        await Assert.That(diagnostics.All(static d => d.Severity == DiagnosticSeverity.Warning)).IsTrue();
        await Assert.That(messages.Count(static m => m.Contains("'ByReference.Id' has the type 'Code', which is compared by reference", StringComparison.Ordinal))).IsEqualTo(1);
        await Assert.That(messages.Count(static m => m.Contains("'ByBootstrapper.Id' has the type 'Code', which is compared by reference", StringComparison.Ordinal))).IsEqualTo(1);
        await Assert.That(messages.Count(static m => m.Contains("'ByUnreadableValueObject.Id' has the type 'Reference', which the default value object serializer cannot read back", StringComparison.Ordinal))).IsEqualTo(1);
    }

    [Test]
    public async Task DoesNotReportANaturalKeyThatRoundTrips()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;
            using dddlib;
            using dddlib.Configuration;

            public partial class ByString : AggregateRoot
            {
                [NaturalKey] public string? Id { get; set; }
            }

            public partial class ByGuid : AggregateRoot
            {
                [NaturalKey] public Guid Id { get; set; }
            }

            public partial class ByRecord : AggregateRoot
            {
                [NaturalKey] public Code? Id { get; set; }
            }

            public partial class ByValueObject : AggregateRoot
            {
                [NaturalKey] public Reference? Id { get; set; }
            }

            public partial class BySerializedValueObject : AggregateRoot
            {
                [NaturalKey] public Checked? Id { get; set; }
            }

            // the natural key of an entity is never serialized
            public partial class Line : Entity
            {
                [NaturalKey] public Plain? Id { get; set; }
            }

            public record Code(string Value);

            public class Plain { }

            public partial class Reference : ValueObject<Reference>
            {
                public Reference(string value) { this.Value = value; }

                public string Value { get; }
            }

            public partial class Checked : ValueObject<Checked>
            {
                public Checked(string value, int checksum) { this.Value = value + checksum; }

                public string Value { get; }
            }

            internal sealed class Bootstrapper : IBootstrapper
            {
                public void Bootstrap(IConfiguration configure)
                {
                    configure.ValueObject<Checked>().ToUseValueObjectSerializer(value => value.Value, text => new Checked(text, 0));
                }
            }
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task ReportsAValueObjectPropertyComparedByReference()
    {
        var diagnostics = await AnalyzeAsync("""
            using System.Collections.Generic;
            using dddlib;

            public partial class Address : ValueObject<Address>
            {
                public string? Street { get; set; }
                public Country? Country { get; set; }
                public List<string>? Lines { get; set; }
                public Postcode? Postcode { get; set; }
                public Region? Region { get; set; }
                public object? Tag { get; set; }
                public int Number { get; set; }
            }

            public class Country
            {
                public string? Name { get; set; }
            }

            public partial class Postcode : ValueObject<Postcode>
            {
                public string? Value { get; set; }
            }

            public record Region(string Name);
            """);

        await Assert.That(diagnostics).Count().IsEqualTo(1);
        await Assert.That(diagnostics[0].Id).IsEqualTo("DDDLIB019");
        await Assert.That(diagnostics[0].Severity).IsEqualTo(DiagnosticSeverity.Warning);
        await Assert.That(diagnostics[0].GetMessage(CultureInfo.InvariantCulture)).Contains("The property 'Country' of the value object 'Address' has the type 'Country'");
    }

    [Test]
    public async Task DoesNotReportAValueObjectPropertyWhenAComparerIsConfigured()
    {
        var diagnostics = await AnalyzeAsync("""
            using System.Collections.Generic;
            using dddlib;
            using dddlib.Configuration;

            public partial class Address : ValueObject<Address>
            {
                public Country? Country { get; set; }
            }

            public class Country
            {
                public string? Name { get; set; }
            }

            internal sealed class Bootstrapper : IBootstrapper
            {
                public void Bootstrap(IConfiguration configure)
                {
                    configure.ValueObject<Address>().ToUseEqualityComparer(EqualityComparer<Address>.Default);
                }
            }
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task ReportsAValueObjectOfAnotherType()
    {
        var diagnostics = await AnalyzeAsync("""
            using dddlib;

            public partial class Money : ValueObject<Money>
            {
                public decimal Amount { get; set; }
            }

            public partial class Price : ValueObject<Money>
            {
                public decimal Amount { get; set; }
            }
            """);

        await Assert.That(diagnostics).Count().IsEqualTo(1);
        await Assert.That(diagnostics[0].Id).IsEqualTo("DDDLIB018");
        await Assert.That(diagnostics[0].Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(diagnostics[0].GetMessage(CultureInfo.InvariantCulture)).Contains("'Price' derives from 'ValueObject<Money>'");
    }

    [Test]
    public async Task DoesNotReportValueObjectsOfThemselves()
    {
        var diagnostics = await AnalyzeAsync("""
            using dddlib;

            public partial class Money : ValueObject<Money>
            {
                public decimal Amount { get; set; }
            }

            public class Wrapper<T> : ValueObject<Wrapper<T>>
            {
                public T? Value { get; set; }
            }

            // a value object by inheritance, compared as its base
            public partial class Salary : Money
            {
            }
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    [Arguments("Entity", "")]
    [Arguments("AggregateRoot", "[NaturalKey] public string? Id { get; set; }")]
    public async Task ReportsASealedEntityOrAggregateRoot(string baseType, string members)
    {
        var diagnostics = await AnalyzeAsync($$"""
            using dddlib;

            public sealed partial class Subject : {{baseType}}
            {
                {{members}}
            }
            """);

        await Assert.That(diagnostics).Count().IsEqualTo(1);
        await Assert.That(diagnostics[0].Id).IsEqualTo("DDDLIB025");
        await Assert.That(diagnostics[0].Severity).IsEqualTo(DiagnosticSeverity.Warning);
        await Assert.That(diagnostics[0].GetMessage(CultureInfo.InvariantCulture)).Contains("'Subject'");
    }

    [Test]
    public async Task DoesNotReportAnUnsealedEntityOrASealedValueObject()
    {
        var diagnostics = await AnalyzeAsync("""
            using dddlib;

            public partial class Thing : Entity
            {
            }

            public partial class Subject : AggregateRoot
            {
                [NaturalKey] public string? Id { get; set; }
            }

            public sealed partial class Money : ValueObject<Money>
            {
                public decimal Amount { get; set; }
            }

            public sealed class Unrelated
            {
            }
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    [Arguments("public partial class", "private", "private")]
    [Arguments("public abstract partial class", "private protected", "private protected")]
    public async Task ReportsAReconstitutionConstructorThatADerivedAggregateRootCannotCall(string declaration, string accessibility, string expected)
    {
        var diagnostics = await AnalyzeAsync($$"""
            using dddlib;

            {{declaration}} Subject : AggregateRoot
            {
                {{accessibility}} Subject() { }

                [NaturalKey] public string? Id { get; set; }
            }
            """);

        await Assert.That(diagnostics).Count().IsEqualTo(1);
        await Assert.That(diagnostics[0].Id).IsEqualTo("DDDLIB027");
        await Assert.That(diagnostics[0].Severity).IsEqualTo(DiagnosticSeverity.Warning);
        await Assert.That(diagnostics[0].GetMessage(CultureInfo.InvariantCulture)).Contains($"'Subject' is {expected}");
    }

    [Test]
    public async Task DoesNotReportAReconstitutionConstructorThatADerivedAggregateRootCanCall()
    {
        var diagnostics = await AnalyzeAsync("""
            using dddlib;

            public partial class Protected : AggregateRoot
            {
                protected Protected() { }

                [NaturalKey] public string? Id { get; set; }
            }

            public partial class ProtectedInternal : AggregateRoot
            {
                protected internal ProtectedInternal() { }

                [NaturalKey] public string? Id { get; set; }
            }

            public partial class Public : AggregateRoot
            {
                public Public() { }

                [NaturalKey] public string? Id { get; set; }
            }

            public partial class Implicit : AggregateRoot
            {
                [NaturalKey] public string? Id { get; set; }
            }

            // an entity is not reconstituted on its own
            public partial class Thing : Entity
            {
                private Thing() { }
            }
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source) =>
        TestCompilation.AnalyzeAsync(source, new DomainTypeAnalyzer());
}
