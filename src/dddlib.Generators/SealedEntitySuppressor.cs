using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace dddlib.Generators;

/// <summary>
/// Suppresses the advice of the .NET analyzers to seal an entity or an aggregate root, which are designed for
/// inheritance (DDDLIB025 reports one that is sealed).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SealedEntitySuppressor : DiagnosticSuppressor
{
    private static readonly SuppressionDescriptor SealInternalTypes = new(
        "DDDLIB026",
        "CA1852",
        "Entities and aggregate roots are designed for inheritance and should not be sealed.");

    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions { get; } = ImmutableArray.Create(SealInternalTypes);

    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        if (KnownSymbols.Create(context.Compilation) is not { } known)
        {
            return;
        }

        foreach (var diagnostic in context.ReportedDiagnostics)
        {
            if (diagnostic.Location.SourceTree is not { } tree)
            {
                continue;
            }

            // CA1852 is reported on the identifier of the type declaration
            var node = tree.GetRoot(context.CancellationToken).FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
            if (context.GetSemanticModel(tree).GetDeclaredSymbol(node, context.CancellationToken) is INamedTypeSymbol type &&
                known.GetDomainTypeKind(type) is DomainTypeKind.AggregateRoot or DomainTypeKind.Entity)
            {
                context.ReportSuppression(Suppression.Create(SealInternalTypes, diagnostic));
            }
        }
    }
}
