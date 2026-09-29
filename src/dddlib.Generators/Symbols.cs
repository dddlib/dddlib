using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace dddlib.Generators;

internal enum DomainTypeKind
{
    None,
    AggregateRoot,
    Entity,
    ValueObject,
}

/// <summary>
/// The dddlib types the generator and analyzers key off, resolved once per compilation.
/// </summary>
internal sealed class KnownSymbols
{
    private KnownSymbols(INamedTypeSymbol aggregateRoot, INamedTypeSymbol entity, INamedTypeSymbol valueObject, INamedTypeSymbol naturalKeyAttribute, INamedTypeSymbol bootstrapper)
    {
        this.AggregateRoot = aggregateRoot;
        this.Entity = entity;
        this.ValueObject = valueObject;
        this.NaturalKeyAttribute = naturalKeyAttribute;
        this.Bootstrapper = bootstrapper;
    }

    public INamedTypeSymbol AggregateRoot { get; }

    public INamedTypeSymbol Entity { get; }

    public INamedTypeSymbol ValueObject { get; }

    public INamedTypeSymbol NaturalKeyAttribute { get; }

    public INamedTypeSymbol Bootstrapper { get; }

    public static KnownSymbols? Create(Compilation compilation)
    {
        var aggregateRoot = compilation.GetTypeByMetadataName("dddlib.AggregateRoot");
        var entity = compilation.GetTypeByMetadataName("dddlib.Entity");
        var valueObject = compilation.GetTypeByMetadataName("dddlib.ValueObject`1");
        var naturalKeyAttribute = compilation.GetTypeByMetadataName("dddlib.NaturalKeyAttribute");
        var bootstrapper = compilation.GetTypeByMetadataName("dddlib.Configuration.IBootstrapper");

        return aggregateRoot is null || entity is null || valueObject is null || naturalKeyAttribute is null || bootstrapper is null
            ? null
            : new KnownSymbols(aggregateRoot, entity, valueObject, naturalKeyAttribute, bootstrapper);
    }

    public DomainTypeKind GetDomainTypeKind(INamedTypeSymbol type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, this.AggregateRoot))
            {
                return DomainTypeKind.AggregateRoot;
            }

            if (SymbolEqualityComparer.Default.Equals(current, this.Entity))
            {
                return DomainTypeKind.Entity;
            }

            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, this.ValueObject))
            {
                return DomainTypeKind.ValueObject;
            }
        }

        return DomainTypeKind.None;
    }

    /// <summary>
    /// Gets the <c>T</c> of the <c>ValueObject&lt;T&gt;</c> the type derives from.
    /// </summary>
    public INamedTypeSymbol? GetValueObjectArgument(INamedTypeSymbol type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, this.ValueObject))
            {
                return current.TypeArguments[0] as INamedTypeSymbol;
            }
        }

        return null;
    }

    public bool IsBootstrapper(INamedTypeSymbol type) =>
        type.TypeKind == TypeKind.Class && !type.IsAbstract && type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, this.Bootstrapper));

    public IEnumerable<IPropertySymbol> GetDeclaredNaturalKeyProperties(INamedTypeSymbol type) =>
        type.GetMembers().OfType<IPropertySymbol>()
            .Where(property => !property.IsStatic && !property.IsIndexer && property.DeclaredAccessibility == Accessibility.Public && property.GetMethod is not null)
            .Where(property => property.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, this.NaturalKeyAttribute)));

    /// <summary>
    /// Gets the public readable properties the default equality comparer would use: declared and inherited, up to
    /// but excluding <c>ValueObject&lt;T&gt;</c>, most derived declaration first.
    /// </summary>
    public IEnumerable<IPropertySymbol> GetValueObjectProperties(INamedTypeSymbol type)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var current = type; current is not null && !SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, this.ValueObject); current = current.BaseType)
        {
            foreach (var property in current.GetMembers().OfType<IPropertySymbol>())
            {
                if (!property.IsStatic && !property.IsIndexer && property.DeclaredAccessibility == Accessibility.Public && property.GetMethod is not null && seen.Add(property.Name))
                {
                    yield return property;
                }
            }
        }
    }
}

internal static class SymbolExtensions
{
    public static readonly SymbolDisplayFormat FullyQualified = SymbolDisplayFormat.FullyQualifiedFormat;

    /// <summary>
    /// Gets the methods that look like event handlers by name and shape, regardless of whether the dispatcher would
    /// accept them; the caller decides what to do with public ones and value-type parameters.
    /// </summary>
    public static IEnumerable<IMethodSymbol> GetHandlerCandidates(this INamedTypeSymbol type) =>
        type.GetMembers().OfType<IMethodSymbol>()
            .Where(method => method.MethodKind == MethodKind.Ordinary && !method.IsStatic && !method.IsGenericMethod)
            .Where(method => string.Equals(method.Name, "Handle", StringComparison.OrdinalIgnoreCase))
            .Where(method => method.Parameters.Length == 1 && method.Parameters[0].RefKind == RefKind.None);

    /// <summary>
    /// The handlers the runtime dispatches to: non-public, single class-typed parameter.
    /// </summary>
    public static IEnumerable<IMethodSymbol> GetDispatchableHandlers(this INamedTypeSymbol type) =>
        type.GetHandlerCandidates()
            .Where(method => method.DeclaredAccessibility != Accessibility.Public)
            .Where(method => method.Parameters[0].Type.IsEventClass());

    public static bool IsEventClass(this ITypeSymbol type) =>
        type.IsReferenceType && type.TypeKind is TypeKind.Class or TypeKind.Delegate or TypeKind.Array;

    public static bool HasParameterlessConstructor(this INamedTypeSymbol type) =>
        !type.IsAbstract && type.InstanceConstructors.Any(constructor => constructor.Parameters.Length == 0);

    public static bool HasPublicParameterlessConstructor(this INamedTypeSymbol type) =>
        !type.IsAbstract && type.InstanceConstructors.Any(constructor => constructor.Parameters.Length == 0 && constructor.DeclaredAccessibility == Accessibility.Public);

    public static bool IsPartial(this INamedTypeSymbol type) =>
        type.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax())
            .OfType<TypeDeclarationSyntax>()
            .Any(declaration => declaration.Modifiers.Any(SyntaxKind.PartialKeyword));

    public static bool IsPartialIncludingContainers(this INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (!current.IsPartial())
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Gets the declaration keyword to reopen the type with in a partial declaration, such as "class" or "record struct".
    /// </summary>
    public static string GetDeclarationKeyword(this INamedTypeSymbol type)
    {
        var declaration = type.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax()).OfType<TypeDeclarationSyntax>().FirstOrDefault();

        return declaration switch
        {
            RecordDeclarationSyntax record when record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword) => "record struct",
            RecordDeclarationSyntax => "record",
            StructDeclarationSyntax => "struct",
            InterfaceDeclarationSyntax => "interface",
            _ => "class",
        };
    }

    public static string GetNameWithTypeParameters(this INamedTypeSymbol type) =>
        type.TypeParameters.Length == 0
            ? type.Name
            : string.Concat(type.Name, "<", string.Join(", ", type.TypeParameters.Select(parameter => parameter.Name)), ">");

    public static string ToNonNullableDisplayString(this ITypeSymbol type) =>
        type.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString(FullyQualified);
}
