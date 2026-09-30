using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace dddlib.Generators;

/// <summary>
/// Reports, at compile time, the model mistakes the runtime would otherwise report as runtime exceptions.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DomainTypeAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        DiagnosticDescriptors.MultipleNaturalKeys,
        DiagnosticDescriptors.ValueTypeEventHandler,
        DiagnosticDescriptors.PublicEventHandler,
        DiagnosticDescriptors.ValueObjectWithoutProperties,
        DiagnosticDescriptors.AbstractEventHandler,
        DiagnosticDescriptors.TypeShouldBePartial);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(static startContext =>
        {
            if (KnownSymbols.Create(startContext.Compilation) is { } known)
            {
                startContext.RegisterSymbolAction(symbolContext => Analyze(symbolContext, known), SymbolKind.NamedType);
            }
        });
    }

    private static void Analyze(SymbolAnalysisContext context, KnownSymbols known)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        var kind = known.GetDomainTypeKind(type);
        if (kind == DomainTypeKind.None)
        {
            return;
        }

        var location = type.Locations.FirstOrDefault() ?? Location.None;

        if (kind is DomainTypeKind.AggregateRoot or DomainTypeKind.Entity && known.GetDeclaredNaturalKeyProperties(type).Skip(1).Any())
        {
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.MultipleNaturalKeys, location, type.ToDisplayString()));
        }

        if (kind == DomainTypeKind.AggregateRoot)
        {
            foreach (var handler in type.GetHandlerCandidates())
            {
                var handlerLocation = handler.Locations.FirstOrDefault() ?? location;
                var parameterType = handler.Parameters[0].Type;

                if (!parameterType.IsEventClass() && parameterType.TypeKind != TypeKind.Interface && parameterType.TypeKind != TypeKind.TypeParameter)
                {
                    context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.ValueTypeEventHandler, handlerLocation, handler.Name, type.ToDisplayString(), parameterType.ToDisplayString()));
                }
                else if (handler.DeclaredAccessibility == Accessibility.Public && parameterType.IsEventClass())
                {
                    context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.PublicEventHandler, handlerLocation, handler.Name, type.ToDisplayString()));
                }
                else if (parameterType.TypeKind == TypeKind.Interface || parameterType is { TypeKind: TypeKind.Class, IsAbstract: true })
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.AbstractEventHandler,
                        handlerLocation,
                        handler.Name,
                        type.ToDisplayString(),
                        parameterType.TypeKind == TypeKind.Interface ? "interface" : "abstract class",
                        parameterType.ToDisplayString()));
                }
            }
        }

        if (kind == DomainTypeKind.ValueObject && !type.IsAbstract && !known.GetValueObjectProperties(type).Any())
        {
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.ValueObjectWithoutProperties, location, type.ToDisplayString()));
        }

        if (!type.IsGenericType && !type.IsPartialIncludingContainers())
        {
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.TypeShouldBePartial, location, type.Name));
        }
    }
}
