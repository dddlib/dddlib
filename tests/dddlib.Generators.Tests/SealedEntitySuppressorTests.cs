using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace dddlib.Generators.Tests;

public class SealedEntitySuppressorTests
{
    [Test]
    public async Task SuppressesTheAdviceToSealAnEntityOrAnAggregateRoot()
    {
        var diagnostics = await TestCompilation.AnalyzeAsync(
            """
            using dddlib;

            internal partial class Thing : Entity
            {
            }

            internal partial class Subject : AggregateRoot
            {
                [NaturalKey] public string? Id { get; set; }
            }

            internal partial class Money : ValueObject<Money>
            {
                public decimal Amount { get; set; }
            }

            internal class Unrelated
            {
            }
            """,
            new SealInternalTypesAnalyzer(),
            new SealedEntitySuppressor());

        var unsuppressed = diagnostics.Where(static diagnostic => diagnostic.Id == "CA1852").Select(static diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture)).ToList();

        await Assert.That(unsuppressed).IsEquivalentTo(["Money", "Unrelated"]);
    }

    // The analyzer authoring rules apply to analyzers that ship; this one only runs in the test and must use CA1852's id.
#pragma warning disable RS1029, RS1036, RS1041, RS2008

    /// <summary>
    /// Stands in for the .NET analyzer that reports CA1852, which the test compilation does not load: it reports every
    /// class that is not sealed, naming it in the message.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    private sealed class SealInternalTypesAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor Descriptor = new(
            "CA1852", "Seal internal types", "{0}", "Performance", DiagnosticSeverity.Warning, isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Descriptor];

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterSymbolAction(
                static symbolContext =>
                {
                    if (symbolContext.Symbol is INamedTypeSymbol { TypeKind: TypeKind.Class, IsSealed: false } type)
                    {
                        symbolContext.ReportDiagnostic(Diagnostic.Create(Descriptor, type.Locations[0], type.Name));
                    }
                },
                SymbolKind.NamedType);
        }
    }

#pragma warning restore RS1029, RS1036, RS1041, RS2008
}
