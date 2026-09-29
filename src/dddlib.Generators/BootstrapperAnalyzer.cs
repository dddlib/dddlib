using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace dddlib.Generators;

/// <summary>
/// Reports bootstrapper declarations the runtime would reject: more than one per assembly, or one without a public
/// default constructor.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BootstrapperAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        DiagnosticDescriptors.MultipleBootstrappers,
        DiagnosticDescriptors.BootstrapperWithoutDefaultConstructor);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(static startContext =>
        {
            if (KnownSymbols.Create(startContext.Compilation) is not { } known)
            {
                return;
            }

            var bootstrappers = new ConcurrentBag<INamedTypeSymbol>();

            startContext.RegisterSymbolAction(
                symbolContext =>
                {
                    var type = (INamedTypeSymbol)symbolContext.Symbol;
                    if (!known.IsBootstrapper(type))
                    {
                        return;
                    }

                    bootstrappers.Add(type);

                    if (!type.HasPublicParameterlessConstructor())
                    {
                        symbolContext.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.BootstrapperWithoutDefaultConstructor,
                            type.Locations.FirstOrDefault() ?? Location.None,
                            type.ToDisplayString()));
                    }
                },
                SymbolKind.NamedType);

            startContext.RegisterCompilationEndAction(endContext =>
            {
                if (bootstrappers.Count <= 1)
                {
                    return;
                }

                foreach (var type in bootstrappers)
                {
                    endContext.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.MultipleBootstrappers,
                        type.Locations.FirstOrDefault() ?? Location.None,
                        endContext.Compilation.AssemblyName));
                }
            });
        });
    }
}
