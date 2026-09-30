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
    private KnownSymbols()
    {
    }

    public required INamedTypeSymbol AggregateRoot { get; init; }

    public required INamedTypeSymbol Entity { get; init; }

    public required INamedTypeSymbol ValueObject { get; init; }

    public required INamedTypeSymbol NaturalKeyAttribute { get; init; }

    public required INamedTypeSymbol BusinessException { get; init; }

    public required INamedTypeSymbol Bootstrapper { get; init; }

    public required INamedTypeSymbol Configuration { get; init; }

    public required INamedTypeSymbol AggregateRootConfigurationWrapper { get; init; }

    public required INamedTypeSymbol EntityConfigurationWrapper { get; init; }

    public required INamedTypeSymbol ValueObjectConfigurationWrapper { get; init; }

    public required INamedTypeSymbol MapperProvider { get; init; }

    public required INamedTypeSymbol EventMapper { get; init; }

    public required INamedTypeSymbol EntityMapper { get; init; }

    public required INamedTypeSymbol ValueObjectMapper { get; init; }

    /// <summary>
    /// Gets <c>IBootstrapper.Bootstrap(IConfiguration)</c>.
    /// </summary>
    public required IMethodSymbol Bootstrap { get; init; }

    /// <summary>
    /// Gets <c>AggregateRoot.Apply&lt;T&gt;(T)</c>.
    /// </summary>
    public required IMethodSymbol Apply { get; init; }

    public required IMethodSymbol GetState { get; init; }

    public required IMethodSymbol SetState { get; init; }

    public static KnownSymbols? Create(Compilation compilation)
    {
        if (compilation.GetTypeByMetadataName("dddlib.AggregateRoot") is not { } aggregateRoot ||
            compilation.GetTypeByMetadataName("dddlib.Entity") is not { } entity ||
            compilation.GetTypeByMetadataName("dddlib.ValueObject`1") is not { } valueObject ||
            compilation.GetTypeByMetadataName("dddlib.NaturalKeyAttribute") is not { } naturalKeyAttribute ||
            compilation.GetTypeByMetadataName("dddlib.BusinessException") is not { } businessException ||
            compilation.GetTypeByMetadataName("dddlib.Configuration.IBootstrapper") is not { } bootstrapper ||
            compilation.GetTypeByMetadataName("dddlib.Configuration.IConfiguration") is not { } configuration ||
            compilation.GetTypeByMetadataName("dddlib.Configuration.IAggregateRootConfigurationWrapper`1") is not { } aggregateRootConfigurationWrapper ||
            compilation.GetTypeByMetadataName("dddlib.Configuration.IEntityConfigurationWrapper`1") is not { } entityConfigurationWrapper ||
            compilation.GetTypeByMetadataName("dddlib.Configuration.IValueObjectConfigurationWrapper`1") is not { } valueObjectConfigurationWrapper ||
            compilation.GetTypeByMetadataName("dddlib.Runtime.IMapperProvider") is not { } mapperProvider ||
            compilation.GetTypeByMetadataName("dddlib.Runtime.IEventMapper`1") is not { } eventMapper ||
            compilation.GetTypeByMetadataName("dddlib.Runtime.IEntityMapper`1") is not { } entityMapper ||
            compilation.GetTypeByMetadataName("dddlib.Runtime.IValueObjectMapper`1") is not { } valueObjectMapper ||
            FindMethod(bootstrapper, "Bootstrap", static method => method.Parameters.Length == 1) is not { } bootstrap ||
            FindMethod(aggregateRoot, "Apply", static method => method.IsGenericMethod && method.Parameters.Length == 1) is not { } apply ||
            FindMethod(aggregateRoot, "GetState", static method => method.IsVirtual && method.Parameters.Length == 0) is not { } getState ||
            FindMethod(aggregateRoot, "SetState", static method => method.IsVirtual && method.Parameters.Length == 1) is not { } setState)
        {
            return null;
        }

        return new KnownSymbols
        {
            AggregateRoot = aggregateRoot,
            Entity = entity,
            ValueObject = valueObject,
            NaturalKeyAttribute = naturalKeyAttribute,
            BusinessException = businessException,
            Bootstrapper = bootstrapper,
            Configuration = configuration,
            AggregateRootConfigurationWrapper = aggregateRootConfigurationWrapper,
            EntityConfigurationWrapper = entityConfigurationWrapper,
            ValueObjectConfigurationWrapper = valueObjectConfigurationWrapper,
            MapperProvider = mapperProvider,
            EventMapper = eventMapper,
            EntityMapper = entityMapper,
            ValueObjectMapper = valueObjectMapper,
            Bootstrap = bootstrap,
            Apply = apply,
            GetState = getState,
            SetState = setState,
        };

        static IMethodSymbol? FindMethod(INamedTypeSymbol type, string name, Func<IMethodSymbol, bool> predicate) =>
            type.GetMembers(name).OfType<IMethodSymbol>().FirstOrDefault(predicate);
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

    /// <summary>
    /// Gets every type declared in the namespace and the namespaces below it, nested types included.
    /// </summary>
    public static IEnumerable<INamedTypeSymbol> GetAllTypes(this INamespaceSymbol @namespace)
    {
        var pending = new Stack<INamespaceOrTypeSymbol>();
        pending.Push(@namespace);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (current is INamedTypeSymbol type)
            {
                yield return type;
            }

            foreach (var member in current.GetMembers())
            {
                if (member is INamespaceOrTypeSymbol namespaceOrType)
                {
                    pending.Push(namespaceOrType);
                }
            }
        }
    }

    /// <summary>
    /// Whether two instances of the type with the same content are equal: strings, value types, and types that
    /// override <c>Equals(object)</c> (value objects, entities and records among them) or implement
    /// <c>IEquatable&lt;T&gt;</c> of themselves. Any other type is compared by reference, or not known until runtime.
    /// </summary>
    public static bool HasValueEquality(this ITypeSymbol type)
    {
        if (type.IsValueType || type.SpecialType == SpecialType.System_String)
        {
            return true;
        }

        if (type.TypeKind != TypeKind.Class)
        {
            return false;
        }

        for (var current = type; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
        {
            if (current.GetMembers("Equals").OfType<IMethodSymbol>().Any(static method =>
                method.IsOverride && method.Parameters.Length == 1 && method.Parameters[0].Type.SpecialType == SpecialType.System_Object))
            {
                return true;
            }
        }

        return type.AllInterfaces.Any(@interface =>
            @interface is { Name: "IEquatable", TypeArguments.Length: 1, ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } } &&
            SymbolEqualityComparer.Default.Equals(@interface.TypeArguments[0], type));
    }

    /// <summary>
    /// Gets the public readable instance properties of the type, declared and inherited, most derived declaration first.
    /// </summary>
    public static IEnumerable<IPropertySymbol> GetPublicReadableProperties(this INamedTypeSymbol type)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var current = type; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
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

    /// <summary>
    /// Whether System.Text.Json, as dddlib configures it, reads back everything it writes for the type: through a
    /// public parameterless constructor and settable properties, or through a single public constructor whose
    /// parameters match the public properties by name, ignoring case.
    /// </summary>
    public static DefaultSerializability IsDefaultSerializable(this INamedTypeSymbol type)
    {
        if (type.IsAbstract || type.TypeKind is not (TypeKind.Class or TypeKind.Struct))
        {
            return DefaultSerializability.NoConstructor;
        }

        var publicConstructors = type.InstanceConstructors.Where(static constructor => constructor.DeclaredAccessibility == Accessibility.Public).ToList();
        var constructor =
            type.InstanceConstructors.FirstOrDefault(static constructor => constructor.HasJsonAttribute("JsonConstructorAttribute")) ??
            publicConstructors.FirstOrDefault(static constructor => constructor.Parameters.Length == 0) ??
            (publicConstructors.Count == 1 ? publicConstructors[0] : null);

        if (constructor is null)
        {
            return DefaultSerializability.NoConstructor;
        }

        var properties = type.GetPublicReadableProperties()
            .Where(static property => !property.HasJsonAttribute("JsonIgnoreAttribute"))
            .ToList();

        foreach (var parameter in constructor.Parameters)
        {
            if (!properties.Any(property => string.Equals(property.Name, parameter.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return new DefaultSerializability(false, null, parameter);
            }
        }

        foreach (var property in properties)
        {
            var isSet = property.SetMethod is { } setter && (setter.DeclaredAccessibility == Accessibility.Public || property.HasJsonAttribute("JsonIncludeAttribute"));
            if (!isSet &&
                property.IsStored() &&
                !constructor.Parameters.Any(parameter => string.Equals(property.Name, parameter.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return new DefaultSerializability(false, property, null);
            }
        }

        return new DefaultSerializability(true, null, null);
    }

    /// <summary>
    /// Whether the property holds state of its own, as opposed to computing its value from other members. A computed
    /// property is written by the serializer but there is nothing to load.
    /// </summary>
    private static bool IsStored(this IPropertySymbol property) =>
        property.SetMethod is not null ||
        property.ContainingType.GetMembers().OfType<IFieldSymbol>().Any(field => SymbolEqualityComparer.Default.Equals(field.AssociatedSymbol, property));

    private static bool HasJsonAttribute(this ISymbol symbol, string name) =>
        symbol.GetAttributes().Any(attribute =>
            attribute.AttributeClass is { } attributeClass &&
            attributeClass.Name == name &&
            attributeClass.ContainingNamespace.ToDisplayString() == "System.Text.Json.Serialization");
}

/// <summary>
/// The outcome of <see cref="SymbolExtensions.IsDefaultSerializable"/>. When the type is not serializable, either the
/// property that is written but never loaded, or the constructor parameter that matches no property, is given; both
/// are <see langword="null"/> when the type has no constructor the serializer can use.
/// </summary>
internal readonly struct DefaultSerializability(bool isSerializable, IPropertySymbol? unloadedProperty, IParameterSymbol? unboundParameter)
{
    public static readonly DefaultSerializability NoConstructor = new(false, null, null);

    public bool IsSerializable { get; } = isSerializable;

    public IPropertySymbol? UnloadedProperty { get; } = unloadedProperty;

    public IParameterSymbol? UnboundParameter { get; } = unboundParameter;
}
