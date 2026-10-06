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
        DiagnosticDescriptors.NaturalKeyDoesNotRoundTrip,
        DiagnosticDescriptors.ValueObjectOfAnotherType,
        DiagnosticDescriptors.ValueObjectPropertyComparedByReference,
        DiagnosticDescriptors.TypeShouldBePartial,
        DiagnosticDescriptors.SealedEntity,
        DiagnosticDescriptors.InaccessibleReconstitutionConstructor);

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

        if (kind == DomainTypeKind.AggregateRoot)
        {
            AnalyzeNaturalKeyTypes(context, type, known, bootstrapper);
        }

        if (kind is DomainTypeKind.AggregateRoot or DomainTypeKind.Entity && type.IsSealed)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.SealedEntity,
                location,
                kind == DomainTypeKind.AggregateRoot ? "aggregate root" : "entity",
                type.ToDisplayString()));
        }

        // a sealed aggregate root is reported by DDDLIB025 instead
        if (kind == DomainTypeKind.AggregateRoot &&
            !type.IsSealed &&
            type.InstanceConstructors.FirstOrDefault(static constructor => constructor.Parameters.Length == 0) is
                { DeclaredAccessibility: Accessibility.Private or Accessibility.ProtectedAndInternal } constructor)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.InaccessibleReconstitutionConstructor,
                constructor.Locations.FirstOrDefault() ?? location,
                type.ToDisplayString(),
                constructor.DeclaredAccessibility == Accessibility.Private ? "private" : "private protected"));
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

        if (kind == DomainTypeKind.ValueObject)
        {
            // Under the default comparer a property is compared with the equality of its own type.
            var comparedByReference = known.GetValueObjectProperties(type)
                .Where(property => SymbolEqualityComparer.Default.Equals(property.ContainingType, type))
                .Where(static property => property.Type is { TypeKind: TypeKind.Class, SpecialType: SpecialType.None } propertyType && !propertyType.IsEnumerable() && !propertyType.HasValueEquality())
                .ToList();

            if (comparedByReference.Count > 0 && !(bootstrapper.Value.IsKnown && bootstrapper.Value.GetConfiguration(type).HasEqualityComparer))
            {
                foreach (var property in comparedByReference)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.ValueObjectPropertyComparedByReference,
                        property.Locations.FirstOrDefault() ?? location,
                        property.Name,
                        type.ToDisplayString(),
                        property.Type.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString()));
                }
            }
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
    /// Checks the type of every natural key the aggregate root type itself declares, by attribute or in the
    /// bootstrapper: the key is serialized when the aggregate root is saved and must equal itself when read back.
    /// </summary>
    private static void AnalyzeNaturalKeyTypes(SymbolAnalysisContext context, INamedTypeSymbol type, KnownSymbols known, Lazy<BootstrapperModel> bootstrapper)
    {
        var declared = known.GetDeclaredNaturalKeyProperties(type).ToList();

        foreach (var property in declared)
        {
            Check(property, property.Locations.FirstOrDefault() ?? Location.None);
        }

        // Only a class or a value object can be reported, so most types never need the bootstrapper read for this.
        if (type.GetMembers().OfType<IPropertySymbol>().Any(static property => property.Type.TypeKind == TypeKind.Class && property.Type.SpecialType == SpecialType.None) &&
            bootstrapper.Value.IsKnown)
        {
            foreach (var selection in bootstrapper.Value.GetConfiguration(type).NaturalKeys)
            {
                if (selection.Property is { } property && !declared.Contains(property, SymbolEqualityComparer.Default))
                {
                    Check(property, selection.Location);
                }
            }
        }

        void Check(IPropertySymbol property, Location location)
        {
            if (property.Type is not INamedTypeSymbol { TypeKind: TypeKind.Class, SpecialType: SpecialType.None } keyType)
            {
                return;
            }

            string? problem = null;
            if (known.GetDomainTypeKind(keyType) == DomainTypeKind.ValueObject)
            {
                if (!keyType.IsDefaultSerializable().IsSerializable &&
                    bootstrapper.Value.IsKnown &&
                    !bootstrapper.Value.GetConfiguration(keyType).HasValueObjectSerializer)
                {
                    problem = "the default value object serializer cannot read back; give it a constructor whose parameters match its public properties, or settable properties, or call ToUseValueObjectSerializer in the bootstrapper";
                }
            }
            else if (!keyType.HasValueEquality())
            {
                problem = "is compared by reference, so a loaded key never equals the key it was saved as; use a string, a value type or a value object";
            }

            if (problem is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.NaturalKeyDoesNotRoundTrip,
                    location,
                    property.ToDisplayString(),
                    keyType.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString(),
                    problem));
            }
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
