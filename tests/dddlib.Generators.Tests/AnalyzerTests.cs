using System.Globalization;
using Microsoft.CodeAnalysis;

namespace dddlib.Generators.Tests;

public class AnalyzerTests
{
    [Test]
    public async Task ReportsMultipleNaturalKeys()
    {
        var diagnostics = await TestCompilation.AnalyzeAsync("""
            using dddlib;

            public partial class Thing : Entity
            {
                [NaturalKey] public string? A { get; set; }
                [NaturalKey] public string? B { get; set; }
            }
            """, new DomainTypeAnalyzer());

        await Assert.That(diagnostics.Select(static d => d.Id)).Contains("DDDLIB001");
    }

    [Test]
    public async Task DoesNotReportSingleNaturalKey()
    {
        var diagnostics = await TestCompilation.AnalyzeAsync("""
            using dddlib;

            public partial class Thing : Entity
            {
                [NaturalKey] public string? A { get; set; }
            }
            """, new DomainTypeAnalyzer());

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task ReportsValueTypeAndPublicHandlers()
    {
        var diagnostics = await TestCompilation.AnalyzeAsync("""
            using dddlib;

            public partial class Subject : AggregateRoot
            {
                private void Handle(int @event) { }
                public void Handle(Visible @event) { }
                private void Handle(Hidden @event) { }
            }

            public class Visible { }
            public class Hidden { }
            """, new DomainTypeAnalyzer());

        await Assert.That(diagnostics.Count(static d => d.Id == "DDDLIB002")).IsEqualTo(1);
        await Assert.That(diagnostics.Count(static d => d.Id == "DDDLIB003")).IsEqualTo(1);
    }

    [Test]
    public async Task ReportsHandlersForAbstractClassesAndInterfaces()
    {
        var diagnostics = await TestCompilation.AnalyzeAsync("""
            using dddlib;

            public partial class Subject : AggregateRoot
            {
                private void Handle(Changed @event) { }
                private void Handle(IChanged @event) { }
                private void Handle(Renamed @event) { }
            }

            public abstract class Changed { }
            public interface IChanged { }
            public class Renamed : Changed, IChanged { }
            """, new DomainTypeAnalyzer());

        var neverCalled = diagnostics.Where(static d => d.Id == "DDDLIB009").ToList();

        await Assert.That(diagnostics).Count().IsEqualTo(2);
        await Assert.That(neverCalled).Count().IsEqualTo(2);
        await Assert.That(neverCalled.Count(static d => d.GetMessage(CultureInfo.InvariantCulture).Contains("abstract class 'Changed'", StringComparison.Ordinal))).IsEqualTo(1);
        await Assert.That(neverCalled.Count(static d => d.GetMessage(CultureInfo.InvariantCulture).Contains("interface 'IChanged'", StringComparison.Ordinal))).IsEqualTo(1);
    }

    [Test]
    public async Task DoesNotReportAHandlerForATypeParameter()
    {
        var diagnostics = await TestCompilation.AnalyzeAsync("""
            using dddlib;

            public abstract class Subject<T> : AggregateRoot
                where T : class
            {
                private void Handle(T @event) { }
            }
            """, new DomainTypeAnalyzer());

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task ReportsValueObjectWithoutPublicProperties()
    {
        var diagnostics = await TestCompilation.AnalyzeAsync("""
            using dddlib;

            public partial class Opaque : ValueObject<Opaque>
            {
                private readonly string value = "x";
            }

            public partial class Clear : ValueObject<Clear>
            {
                public string? Value { get; set; }
            }
            """, new DomainTypeAnalyzer());

        var withoutProperties = diagnostics.Where(static d => d.Id == "DDDLIB004").ToList();

        await Assert.That(withoutProperties).Count().IsEqualTo(1);
        await Assert.That(withoutProperties[0].GetMessage(CultureInfo.InvariantCulture)).Contains("Opaque");
    }

    [Test]
    public async Task ReportsTypesThatCouldBePartial()
    {
        var diagnostics = await TestCompilation.AnalyzeAsync("""
            using dddlib;

            public class Plain : Entity { }

            public partial class Generated : Entity { }
            """, new DomainTypeAnalyzer());

        var hints = diagnostics.Where(static d => d.Id == "DDDLIB007").ToList();

        await Assert.That(hints).Count().IsEqualTo(1);
        await Assert.That(hints[0].Severity).IsEqualTo(DiagnosticSeverity.Info);
        await Assert.That(hints[0].GetMessage(CultureInfo.InvariantCulture)).Contains("Plain");
    }

    [Test]
    public async Task ReportsMultipleBootstrappers()
    {
        var diagnostics = await TestCompilation.AnalyzeAsync("""
            using dddlib.Configuration;

            public class First : IBootstrapper { public void Bootstrap(IConfiguration configure) { } }
            public class Second : IBootstrapper { public void Bootstrap(IConfiguration configure) { } }
            """, new BootstrapperAnalyzer());

        await Assert.That(diagnostics.Count(static d => d.Id == "DDDLIB005")).IsEqualTo(2);
    }

    [Test]
    public async Task ReportsBootstrapperWithoutDefaultConstructor()
    {
        var diagnostics = await TestCompilation.AnalyzeAsync("""
            using dddlib.Configuration;

            public class Bootstrapper : IBootstrapper
            {
                public Bootstrapper(string irrelevant) { }
                public void Bootstrap(IConfiguration configure) { }
            }
            """, new BootstrapperAnalyzer());

        await Assert.That(diagnostics.Select(static d => d.Id)).Contains("DDDLIB006");
    }

    [Test]
    public async Task DoesNotReportAWellFormedBootstrapper()
    {
        var diagnostics = await TestCompilation.AnalyzeAsync("""
            using dddlib.Configuration;

            internal sealed class Bootstrapper : IBootstrapper { public void Bootstrap(IConfiguration configure) { } }
            """, new BootstrapperAnalyzer());

        await Assert.That(diagnostics).IsEmpty();
    }
}
