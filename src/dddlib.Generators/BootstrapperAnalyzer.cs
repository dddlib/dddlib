using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace dddlib.Generators;

/// <summary>
/// Reports bootstrapper declarations the runtime would reject: more than one per assembly, or one without a public
/// default constructor. Also reports what the bootstrapper configures wrongly, and the mappings it leaves out.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BootstrapperAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        DiagnosticDescriptors.MultipleBootstrappers,
        DiagnosticDescriptors.BootstrapperWithoutDefaultConstructor,
        DiagnosticDescriptors.MappingNotConfigured,
        DiagnosticDescriptors.ConflictingNaturalKeySelector,
        DiagnosticDescriptors.InvalidNaturalKeySelector);

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
            var model = BootstrapperModel.GetLazy(startContext.Compilation, known);

            startContext.RegisterOperationAction(operationContext => AnalyzeMapping(operationContext, known, model), OperationKind.Invocation);

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

                    AnalyzeNaturalKeySelectors(symbolContext, known, model);
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

    /// <summary>
    /// Reports the natural key selectors of the bootstrapper the runtime rejects. A known model has one bootstrapper,
    /// so this runs once, from the symbol action of that bootstrapper.
    /// </summary>
    private static void AnalyzeNaturalKeySelectors(SymbolAnalysisContext context, KnownSymbols known, Lazy<BootstrapperModel> model)
    {
        if (!model.Value.IsKnown)
        {
            return;
        }

        foreach (var configuration in model.Value.Configurations)
        {
            var declared = known.GetDeclaredNaturalKeyProperties(configuration.Key).ToList();

            foreach (var selection in configuration.Value.NaturalKeys)
            {
                if (selection.IsLambda && selection.Property is null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.InvalidNaturalKeySelector, selection.Location));
                }
                else if (selection.Property is { } selected && declared.Count == 1 && !SymbolEqualityComparer.Default.Equals(selected, declared[0]))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.ConflictingNaturalKeySelector,
                        selection.Location,
                        selected.Name,
                        configuration.Key.ToDisplayString(),
                        declared[0].Name));
                }
            }
        }
    }

    private static void AnalyzeMapping(OperationAnalysisContext context, KnownSymbols known, Lazy<BootstrapperModel> model)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;
        var mapper = method.ContainingType.OriginalDefinition;

        ITypeSymbol mapped;
        ITypeSymbol @event;
        bool isReverse;

        if (method.Name == "ToEvent" &&
            (SymbolEqualityComparer.Default.Equals(mapper, known.EntityMapper) || SymbolEqualityComparer.Default.Equals(mapper, known.ValueObjectMapper)))
        {
            (mapped, @event, isReverse) = (method.ContainingType.TypeArguments[0], method.TypeArguments[0], false);
        }
        else if (method.Name is "ToEntity" or "ToValueObject" && SymbolEqualityComparer.Default.Equals(mapper, known.EventMapper))
        {
            (mapped, @event, isReverse) = (method.TypeArguments[0], method.ContainingType.TypeArguments[0], true);
        }
        else
        {
            return;
        }

        if (mapped is not INamedTypeSymbol || @event is not INamedTypeSymbol || !model.Value.IsKnown)
        {
            return;
        }

        // The mapping is looked up by the runtime type of what Map is given, which may derive from the type seen here.
        var isConfigured = model.Value.Configurations.Any(configuration =>
            (isReverse ? SymbolEqualityComparer.Default.Equals(configuration.Key, mapped) : configuration.Key.IsOrDerivesFrom(mapped)) &&
            configuration.Value.EventMappings.Any(mapping =>
                isReverse ? mapping.Value && mapping.Key.IsOrDerivesFrom(@event) : SymbolEqualityComparer.Default.Equals(mapping.Key, @event)));

        if (!isConfigured)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.MappingNotConfigured,
                invocation.Syntax.GetLocation(),
                (isReverse ? @event : mapped).ToDisplayString(),
                (isReverse ? mapped : @event).ToDisplayString(),
                $"ToMapToEvent<{@event.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}> for '{mapped.ToDisplayString()}'{(isReverse ? " with a reverse mapping" : string.Empty)}"));
        }
    }
}
