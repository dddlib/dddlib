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
        DiagnosticDescriptors.IncompleteMemento,
        DiagnosticDescriptors.NoReconstitutionFactory,
        DiagnosticDescriptors.NoNaturalKey,
        DiagnosticDescriptors.IgnoredNaturalKey,
        DiagnosticDescriptors.ValueObjectOfAnotherType,
        DiagnosticDescriptors.TypeShouldBePartial);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(static startContext =>
        {
            if (KnownSymbols.Create(startContext.Compilation) is { } known)
            {
                var bootstrapper = BootstrapperModel.GetLazy(startContext.Compilation, known);

                startContext.RegisterSymbolAction(symbolContext => Analyze(symbolContext, known, bootstrapper), SymbolKind.NamedType);
                startContext.RegisterSymbolAction(symbolContext => AnalyzeProperty(symbolContext, known), SymbolKind.Property);
            }
        });
    }

    private static void Analyze(SymbolAnalysisContext context, KnownSymbols known, Lazy<BootstrapperModel> bootstrapper)
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

        if (kind == DomainTypeKind.AggregateRoot && !type.IsAbstract)
        {
            var getState = type.FindOverride(known.GetState);
            var setState = type.FindOverride(known.SetState);

            if ((getState ?? setState) is { } only && (getState is null || setState is null))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.IncompleteMemento,
                    SymbolEqualityComparer.Default.Equals(only.ContainingType, type) ? only.Locations.FirstOrDefault() ?? location : location,
                    type.ToDisplayString(),
                    only.Name,
                    getState is null ? known.GetState.Name : known.SetState.Name,
                    getState is null ? "saved" : "loaded"));
            }

            if (!type.IsGenericType &&
                !type.InstanceConstructors.Any(static constructor => constructor.Parameters.Length == 0) &&
                bootstrapper.Value.IsKnown &&
                !bootstrapper.Value.GetConfiguration(type).HasReconstitutionFactory)
            {
                context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.NoReconstitutionFactory, location, type.ToDisplayString()));
            }

            if (!type.IsGenericType && !HasNaturalKey(type, known, bootstrapper))
            {
                context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.NoNaturalKey, location, type.ToDisplayString()));
            }
        }

        if (kind == DomainTypeKind.ValueObject &&
            type.BaseType is { } valueObject &&
            SymbolEqualityComparer.Default.Equals(valueObject.OriginalDefinition, known.ValueObject) &&
            !SymbolEqualityComparer.Default.Equals(valueObject.TypeArguments[0], type))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ValueObjectOfAnotherType,
                location,
                type.ToDisplayString(),
                valueObject.TypeArguments[0].ToDisplayString()));
        }

        if (kind == DomainTypeKind.ValueObject &&
            !type.IsAbstract &&
            !known.GetValueObjectProperties(type).Any() &&
            !(bootstrapper.Value.IsKnown && bootstrapper.Value.GetConfiguration(type).HasEqualityComparer))
        {
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.ValueObjectWithoutProperties, location, type.ToDisplayString()));
        }

        if (!type.IsGenericType && !type.IsPartialIncludingContainers())
        {
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.TypeShouldBePartial, location, type.Name));
        }
    }

    /// <summary>
    /// Whether the aggregate root has, or may have, a natural key: an attribute anywhere in its hierarchy, or a
    /// selector in the bootstrapper, which cannot be ruled out when the bootstrapper model is unknown.
    /// </summary>
    private static bool HasNaturalKey(INamedTypeSymbol type, KnownSymbols known, Lazy<BootstrapperModel> bootstrapper)
    {
        for (var current = type; current is not null && !SymbolEqualityComparer.Default.Equals(current, known.Entity); current = current.BaseType)
        {
            if (known.GetDeclaredNaturalKeyProperties(current).Any())
            {
                return true;
            }
        }

        if (!bootstrapper.Value.IsKnown)
        {
            return true;
        }

        for (var current = type; current is not null && !SymbolEqualityComparer.Default.Equals(current, known.Entity); current = current.BaseType)
        {
            if (!bootstrapper.Value.GetConfiguration(current).NaturalKeys.IsEmpty)
            {
                return true;
            }
        }

        return false;
    }

    private static void AnalyzeProperty(SymbolAnalysisContext context, KnownSymbols known)
    {
        var property = (IPropertySymbol)context.Symbol;
        var attribute = property.GetAttributes().FirstOrDefault(candidate => SymbolEqualityComparer.Default.Equals(candidate.AttributeClass, known.NaturalKeyAttribute));
        if (attribute is null)
        {
            return;
        }

        // the same conditions as KnownSymbols.GetDeclaredNaturalKeyProperties, and the runtime
        var reason =
            known.GetDomainTypeKind(property.ContainingType) is not (DomainTypeKind.Entity or DomainTypeKind.AggregateRoot) ? $"'{property.ContainingType.ToDisplayString()}' is not an entity or an aggregate root"
            : property.IsStatic ? "it is static"
            : property.IsIndexer ? "it is an indexer"
            : property.GetMethod is null ? "it has no getter"
            : property.DeclaredAccessibility != Accessibility.Public ? "it is not public"
            : null;

        if (reason is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.IgnoredNaturalKey,
                attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation() ?? property.Locations.FirstOrDefault() ?? Location.None,
                property.ToDisplayString(),
                reason));
        }
    }
}
