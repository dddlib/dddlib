using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;

namespace dddlib.Generators.Tests;

public class BootstrapperAnalyzerTests
{
    private const string Model = """
        using dddlib;
        using dddlib.Configuration;

        public partial class Order : AggregateRoot
        {
            [NaturalKey] public string? Id { get; set; }

            public string? Reference { get; set; }

            public void Add(Line line) => this.Apply(this.Map.Entity(line).ToEvent<LineAdded>());

            public void Price(Money money) => this.Apply(this.Map.ValueObject(money).ToEvent(new Priced()));

            private void Handle(LineAdded @event) => _ = this.Map.Event(@event).ToEntity<Line>();

            private void Handle(Priced @event) => _ = this.Map.Event(@event).ToValueObject<Money>();
        }

        public partial class Line : Entity
        {
            public string? Sku { get; set; }
        }

        public partial class Money : ValueObject<Money>
        {
            public decimal Amount { get; set; }
        }

        public class LineAdded { }

        public class Priced { }

        """;

    [Test]
    public async Task ReportsMappingsTheBootstrapperDoesNotConfigure()
    {
        var diagnostics = await AnalyzeAsync(Model + """
            internal sealed class Bootstrapper : IBootstrapper
            {
                public void Bootstrap(IConfiguration configure)
                {
                    configure.Entity<Line>().ToMapToEvent<LineAdded>((line, @event) => { });
                }
            }
            """);

        var messages = diagnostics.Where(static d => d.Id == "DDDLIB020").Select(static d => d.GetMessage(CultureInfo.InvariantCulture)).ToList();

        await Assert.That(diagnostics).Count().IsEqualTo(3);
        await Assert.That(diagnostics.All(static d => d.Severity == DiagnosticSeverity.Warning)).IsTrue();
        await Assert.That(messages.Count(static m => m.StartsWith("No mapping from 'LineAdded' to 'Line' is configured", StringComparison.Ordinal) && m.Contains("ToMapToEvent<LineAdded> for 'Line' with a reverse mapping", StringComparison.Ordinal))).IsEqualTo(1);
        await Assert.That(messages.Count(static m => m.StartsWith("No mapping from 'Money' to 'Priced' is configured", StringComparison.Ordinal) && m.Contains("ToMapToEvent<Priced> for 'Money' in", StringComparison.Ordinal))).IsEqualTo(1);
        await Assert.That(messages.Count(static m => m.StartsWith("No mapping from 'Priced' to 'Money' is configured", StringComparison.Ordinal))).IsEqualTo(1);
    }

    [Test]
    public async Task ReportsEveryMappingWithoutABootstrapper()
    {
        var diagnostics = await AnalyzeAsync(Model);

        await Assert.That(diagnostics.Count(static d => d.Id == "DDDLIB020")).IsEqualTo(4);
        await Assert.That(diagnostics).Count().IsEqualTo(4);
    }

    [Test]
    public async Task DoesNotReportMappingsTheBootstrapperConfigures()
    {
        var diagnostics = await AnalyzeAsync(Model + """
            internal sealed class Bootstrapper : IBootstrapper
            {
                public void Bootstrap(IConfiguration configure)
                {
                    configure.Entity<Line>().ToMapToEvent<LineAdded>((line, @event) => { }, @event => new Line());
                    configure.ValueObject<Money>().ToMapToEvent<Priced>((money, @event) => { }, @event => new Money());
                }
            }
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task DoesNotReportAMappingConfiguredForADerivedType()
    {
        // the mapping is looked up by the runtime type of the entity, which may be the configured subclass
        var diagnostics = await AnalyzeAsync("""
            using dddlib;
            using dddlib.Configuration;

            public partial class Order : AggregateRoot
            {
                [NaturalKey] public string? Id { get; set; }

                public void Add(Line line) => this.Apply(this.Map.Entity(line).ToEvent<LineAdded>());

                private void Handle(LineAdded @event) { }
            }

            public partial class Line : Entity { }

            public partial class DiscountedLine : Line { }

            public class LineAdded { }

            internal sealed class Bootstrapper : IBootstrapper
            {
                public void Bootstrap(IConfiguration configure)
                {
                    configure.Entity<DiscountedLine>().ToMapToEvent<LineAdded>((line, @event) => { });
                }
            }
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task DoesNotReportMappingsAgainstABootstrapperItCannotRead()
    {
        var diagnostics = await AnalyzeAsync(Model + """
            internal sealed class Bootstrapper : IBootstrapper
            {
                public void Bootstrap(IConfiguration configure) => Configure(configure);

                private static void Configure(IConfiguration configure) { }
            }
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task ReportsABootstrapperThatSelectsADifferentNaturalKey()
    {
        var diagnostics = await AnalyzeAsync(Model + """
            internal sealed class Bootstrapper : IBootstrapper
            {
                public void Bootstrap(IConfiguration configure)
                {
                    configure.AggregateRoot<Order>().ToUseNaturalKey(order => order.Reference);
                    configure.Entity<Line>().ToMapToEvent<LineAdded>((line, @event) => { }, @event => new Line());
                    configure.ValueObject<Money>().ToMapToEvent<Priced>((money, @event) => { }, @event => new Money());
                }
            }
            """);

        await Assert.That(diagnostics).Count().IsEqualTo(1);
        await Assert.That(diagnostics[0].Id).IsEqualTo("DDDLIB021");
        await Assert.That(diagnostics[0].Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(diagnostics[0].GetMessage(CultureInfo.InvariantCulture)).IsEqualTo("The bootstrapper selects 'Reference' as the natural key of 'Order', which declares 'Id' as its natural key with [NaturalKey]");
        await Assert.That(diagnostics[0].Location.SourceTree!.GetText().ToString(diagnostics[0].Location.SourceSpan)).IsEqualTo("order => order.Reference");
    }

    [Test]
    public async Task ReportsANaturalKeySelectorThatIsNotAPropertyOfItsParameter()
    {
        var diagnostics = await AnalyzeAsync(Model + """
            internal sealed class Bootstrapper : IBootstrapper
            {
                public void Bootstrap(IConfiguration configure)
                {
                    configure.Entity<Line>()
                        .ToUseNaturalKey(line => line.Sku + "!")
                        .ToMapToEvent<LineAdded>((line, @event) => { }, @event => new Line());
                    configure.ValueObject<Money>().ToMapToEvent<Priced>((money, @event) => { }, @event => new Money());
                }
            }
            """);

        await Assert.That(diagnostics).Count().IsEqualTo(1);
        await Assert.That(diagnostics[0].Id).IsEqualTo("DDDLIB022");
        await Assert.That(diagnostics[0].Severity).IsEqualTo(DiagnosticSeverity.Error);
    }

    [Test]
    public async Task DoesNotReportANaturalKeySelectorThatAgreesWithTheAttribute()
    {
        var diagnostics = await AnalyzeAsync(Model + """
            internal sealed class Bootstrapper : IBootstrapper
            {
                public void Bootstrap(IConfiguration configure)
                {
                    configure.AggregateRoot<Order>().ToUseNaturalKey(order => order.Id);
                    configure.Entity<Line>()
                        .ToUseNaturalKey(line => line.Sku)
                        .ToMapToEvent<LineAdded>((line, @event) => { }, @event => new Line());
                    configure.ValueObject<Money>().ToMapToEvent<Priced>((money, @event) => { }, @event => new Money());
                }
            }
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source) =>
        TestCompilation.AnalyzeAsync(source, new BootstrapperAnalyzer());
}
