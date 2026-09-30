using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace dddlib.Generators;

/// <summary>
/// What the assembly's bootstrapper configures, read from the body of its <c>Bootstrap</c> method: every fluent chain
/// rooted at <c>configure.AggregateRoot&lt;T&gt;()</c>, <c>configure.Entity&lt;T&gt;()</c> or
/// <c>configure.ValueObject&lt;T&gt;()</c>. An assembly without a bootstrapper configures nothing, and that is known.
/// The model is <see cref="Unknown"/> when configuration may happen somewhere the analyzer cannot follow; rules that
/// depend on the bootstrapper must not report against an unknown model.
/// </summary>
internal sealed class BootstrapperModel
{
    public static readonly BootstrapperModel Unknown = new(isKnown: false, new Dictionary<INamedTypeSymbol, BootstrapperTypeConfiguration>(SymbolEqualityComparer.Default));

    private static readonly ConditionalWeakTable<Compilation, Lazy<BootstrapperModel>> Cache = new();

    private readonly Dictionary<INamedTypeSymbol, BootstrapperTypeConfiguration> configurations;

    private BootstrapperModel(bool isKnown, Dictionary<INamedTypeSymbol, BootstrapperTypeConfiguration> configurations)
    {
        this.IsKnown = isKnown;
        this.configurations = configurations;
    }

    public bool IsKnown { get; }

    /// <summary>
    /// Gets every type the bootstrapper configures, with its configuration.
    /// </summary>
    public IEnumerable<KeyValuePair<INamedTypeSymbol, BootstrapperTypeConfiguration>> Configurations => this.configurations;

    /// <summary>
    /// Gets the model of the compilation, built on first use and shared by every analyzer: symbol actions run
    /// concurrently and most compilations never need it.
    /// </summary>
    public static Lazy<BootstrapperModel> GetLazy(Compilation compilation, KnownSymbols known) =>
        Cache.GetValue(compilation, key => new Lazy<BootstrapperModel>(() => Create(key, known)));

    public static BootstrapperModel Create(Compilation compilation, KnownSymbols known)
    {
        var bootstrapper = default(INamedTypeSymbol);
        var configurationMethods = new List<IMethodSymbol>();

        foreach (var type in compilation.Assembly.GlobalNamespace.GetAllTypes())
        {
            if (known.IsBootstrapper(type))
            {
                if (bootstrapper is not null)
                {
                    return Unknown;
                }

                bootstrapper = type;
            }

            configurationMethods.AddRange(type.GetMembers().OfType<IMethodSymbol>().Where(method =>
                method.Parameters.Any(parameter => SymbolEqualityComparer.Default.Equals(parameter.Type, known.Configuration))));
        }

        var bootstrap = bootstrapper?.FindImplementationForInterfaceMember(known.Bootstrap) as IMethodSymbol;

        // Any other method that takes the configuration may be configuring types too: a helper of the bootstrapper, or
        // a class that a custom bootstrapper provider calls.
        if (configurationMethods.Any(method => !SymbolEqualityComparer.Default.Equals(method, bootstrap)))
        {
            return Unknown;
        }

        if (bootstrapper is null)
        {
            return new BootstrapperModel(isKnown: true, new Dictionary<INamedTypeSymbol, BootstrapperTypeConfiguration>(SymbolEqualityComparer.Default));
        }

        if (bootstrap is null)
        {
            return Unknown;
        }

        if (bootstrap.DeclaringSyntaxReferences.Length != 1)
        {
            return Unknown;
        }

        var declaration = bootstrap.DeclaringSyntaxReferences[0].GetSyntax();
        if (compilation.GetSemanticModel(declaration.SyntaxTree).GetOperation(declaration) is not { } body)
        {
            return Unknown;
        }

        var configure = bootstrap.Parameters[0];
        var builders = new Dictionary<INamedTypeSymbol, Builder>(SymbolEqualityComparer.Default);

        foreach (var reference in body.Descendants().OfType<IParameterReferenceOperation>())
        {
            if (!SymbolEqualityComparer.Default.Equals(reference.Parameter, configure))
            {
                continue;
            }

            // The configuration must only ever be the receiver of AggregateRoot<T>(), Entity<T>() or ValueObject<T>().
            if (reference.Parent is not IInvocationOperation root ||
                root.Instance != reference ||
                !SymbolEqualityComparer.Default.Equals(root.TargetMethod.ContainingType, known.Configuration) ||
                root.TargetMethod.TypeArguments.Length != 1 ||
                root.TargetMethod.TypeArguments[0] is not INamedTypeSymbol type)
            {
                return Unknown;
            }

            if (!builders.TryGetValue(type, out var builder))
            {
                builders.Add(type, builder = new Builder());
            }

            // And the wrapper it returns must only ever be the receiver of the next configuration call in a statement.
            IOperation current = root;
            while (current.Parent is IInvocationOperation next && next.Instance == current)
            {
                if (!builder.TryRecord(next, known))
                {
                    return Unknown;
                }

                current = next;
            }

            if (current.Parent is not IExpressionStatementOperation)
            {
                return Unknown;
            }
        }

        return new BootstrapperModel(
            isKnown: true,
            builders.ToDictionary<KeyValuePair<INamedTypeSymbol, Builder>, INamedTypeSymbol, BootstrapperTypeConfiguration>(
                static pair => pair.Key,
                static pair => pair.Value.Build(),
                SymbolEqualityComparer.Default));
    }

    /// <summary>
    /// Gets what the bootstrapper configures for exactly the specified type; configuration is not inherited.
    /// </summary>
    public BootstrapperTypeConfiguration GetConfiguration(INamedTypeSymbol type) =>
        this.configurations.TryGetValue(type, out var configuration) ? configuration : BootstrapperTypeConfiguration.Empty;

    private sealed class Builder
    {
        private readonly ImmutableArray<NaturalKeySelection>.Builder naturalKeys = ImmutableArray.CreateBuilder<NaturalKeySelection>();
        private readonly Dictionary<ITypeSymbol, EventMappingKinds> eventMappings = new(SymbolEqualityComparer.Default);
        private bool hasReconstitutionFactory;
        private bool hasEqualityComparer;
        private bool hasValueObjectSerializer;

        public bool TryRecord(IInvocationOperation invocation, KnownSymbols known)
        {
            var method = invocation.TargetMethod;
            var wrapper = method.ContainingType.OriginalDefinition;

            if (!SymbolEqualityComparer.Default.Equals(wrapper, known.AggregateRootConfigurationWrapper) &&
                !SymbolEqualityComparer.Default.Equals(wrapper, known.EntityConfigurationWrapper) &&
                !SymbolEqualityComparer.Default.Equals(wrapper, known.ValueObjectConfigurationWrapper))
            {
                return false;
            }

            switch (method.Name)
            {
                case "ToReconstituteUsing":
                    this.hasReconstitutionFactory = true;
                    return true;

                case "ToUseNaturalKey" when invocation.Arguments.Length == 1:
                    this.naturalKeys.Add(NaturalKeySelection.Create(invocation.Arguments[0]));
                    return true;

                case "ToUseEqualityComparer":
                    this.hasEqualityComparer = true;
                    return true;

                case "ToUseValueObjectSerializer":
                    this.hasValueObjectSerializer = true;
                    return true;

                case "ToMapToEvent" when method.TypeArguments.Length == 1 && method.Parameters.Length > 0:
                    // A mapping that takes only the entity or value object creates the event; any other is given it.
                    var kinds = method.Parameters[0].Type is INamedTypeSymbol { DelegateInvokeMethod.Parameters.Length: 1 }
                        ? EventMappingKinds.NewEvent
                        : EventMappingKinds.ExistingEvent;

                    if (method.Parameters.Length == 2)
                    {
                        kinds |= EventMappingKinds.Reverse;
                    }

                    this.eventMappings[method.TypeArguments[0]] = this.eventMappings.TryGetValue(method.TypeArguments[0], out var existing) ? existing | kinds : kinds;
                    return true;

                default:
                    return false;
            }
        }

        public BootstrapperTypeConfiguration Build() =>
            new(this.hasReconstitutionFactory, this.naturalKeys.ToImmutable(), this.hasEqualityComparer, this.hasValueObjectSerializer, this.eventMappings);
    }
}

/// <summary>
/// What the bootstrapper configures for one aggregate root, entity or value object type.
/// </summary>
internal sealed class BootstrapperTypeConfiguration
{
    public static readonly BootstrapperTypeConfiguration Empty = new(false, ImmutableArray<NaturalKeySelection>.Empty, false, false, new Dictionary<ITypeSymbol, EventMappingKinds>(SymbolEqualityComparer.Default));

    private readonly Dictionary<ITypeSymbol, EventMappingKinds> eventMappings;

    public BootstrapperTypeConfiguration(
        bool hasReconstitutionFactory,
        ImmutableArray<NaturalKeySelection> naturalKeys,
        bool hasEqualityComparer,
        bool hasValueObjectSerializer,
        Dictionary<ITypeSymbol, EventMappingKinds> eventMappings)
    {
        this.HasReconstitutionFactory = hasReconstitutionFactory;
        this.NaturalKeys = naturalKeys;
        this.HasEqualityComparer = hasEqualityComparer;
        this.HasValueObjectSerializer = hasValueObjectSerializer;
        this.eventMappings = eventMappings;
    }

    /// <summary>
    /// Gets a value indicating whether <c>ToReconstituteUsing</c> is called.
    /// </summary>
    public bool HasReconstitutionFactory { get; }

    /// <summary>
    /// Gets one entry per <c>ToUseNaturalKey</c> call.
    /// </summary>
    public ImmutableArray<NaturalKeySelection> NaturalKeys { get; }

    /// <summary>
    /// Gets a value indicating whether <c>ToUseEqualityComparer</c> is called.
    /// </summary>
    public bool HasEqualityComparer { get; }

    /// <summary>
    /// Gets a value indicating whether <c>ToUseValueObjectSerializer</c> is called.
    /// </summary>
    public bool HasValueObjectSerializer { get; }

    /// <summary>
    /// Gets the event type of every <c>ToMapToEvent&lt;TEvent&gt;</c> call, and the kinds of mapping those calls configure.
    /// </summary>
    public IEnumerable<KeyValuePair<ITypeSymbol, EventMappingKinds>> EventMappings => this.eventMappings;

    /// <summary>
    /// Whether <c>ToMapToEvent&lt;TEvent&gt;</c> is called for the event type, with or without a reverse mapping.
    /// </summary>
    public bool MapsToEvent(ITypeSymbol @event) => this.eventMappings.ContainsKey(@event);

    /// <summary>
    /// Whether <c>ToMapToEvent&lt;TEvent&gt;</c> is called for the event type with a mapping that creates the event.
    /// </summary>
    public bool MapsToNewEvent(ITypeSymbol @event) => this.Maps(@event, EventMappingKinds.NewEvent);

    /// <summary>
    /// Whether <c>ToMapToEvent&lt;TEvent&gt;</c> is called for the event type with a mapping that is given the event.
    /// </summary>
    public bool MapsToExistingEvent(ITypeSymbol @event) => this.Maps(@event, EventMappingKinds.ExistingEvent);

    /// <summary>
    /// Whether <c>ToMapToEvent&lt;TEvent&gt;</c> is called for the event type with a reverse mapping.
    /// </summary>
    public bool MapsFromEvent(ITypeSymbol @event) => this.Maps(@event, EventMappingKinds.Reverse);

    private bool Maps(ITypeSymbol @event, EventMappingKinds kind) => this.eventMappings.TryGetValue(@event, out var kinds) && (kinds & kind) != 0;
}

/// <summary>
/// The mappings the <c>ToMapToEvent&lt;TEvent&gt;</c> calls for one event type configure between them.
/// </summary>
[Flags]
internal enum EventMappingKinds
{
    None = 0,

    /// <summary>
    /// A mapping that creates the event, used by <c>ToEvent&lt;T&gt;()</c>.
    /// </summary>
    NewEvent = 1,

    /// <summary>
    /// A mapping that is given the event, and changes it or returns a copy of it. Used by <c>ToEvent(@event)</c>, and
    /// by <c>ToEvent&lt;T&gt;()</c> for an event with a public parameterless constructor.
    /// </summary>
    ExistingEvent = 2,

    /// <summary>
    /// A mapping from the event back to the entity or value object.
    /// </summary>
    Reverse = 4,
}

/// <summary>
/// The selector of one <c>ToUseNaturalKey</c> call. <see cref="Property"/> is the selected property when the selector
/// is a lambda whose body is a property of its parameter, which is the only shape the runtime accepts.
/// </summary>
internal sealed class NaturalKeySelection
{
    private NaturalKeySelection(IPropertySymbol? property, bool isLambda, Location location)
    {
        this.Property = property;
        this.IsLambda = isLambda;
        this.Location = location;
    }

    public IPropertySymbol? Property { get; }

    /// <summary>
    /// Gets a value indicating whether the selector is written as a lambda at the call, and so can be read at all.
    /// </summary>
    public bool IsLambda { get; }

    public Location Location { get; }

    public static NaturalKeySelection Create(IArgumentOperation argument)
    {
        var location = argument.Syntax.GetLocation();

        if (Unwrap(argument.Value) is not IAnonymousFunctionOperation lambda)
        {
            return new NaturalKeySelection(null, isLambda: false, location);
        }

        var property =
            lambda.Body.Operations.Length == 1 &&
            lambda.Body.Operations[0] is IReturnOperation { ReturnedValue: IPropertyReferenceOperation { Instance: { } instance } reference } &&
            Unwrap(instance) is IParameterReferenceOperation parameter &&
            lambda.Symbol.Parameters.Length == 1 &&
            SymbolEqualityComparer.Default.Equals(parameter.Parameter, lambda.Symbol.Parameters[0])
                ? reference.Property
                : null;

        return new NaturalKeySelection(property, isLambda: true, location);
    }

    private static IOperation Unwrap(IOperation operation)
    {
        while (true)
        {
            switch (operation)
            {
                case IConversionOperation { IsImplicit: true } conversion:
                    operation = conversion.Operand;
                    break;
                case IDelegateCreationOperation delegateCreation:
                    operation = delegateCreation.Target;
                    break;
                default:
                    return operation;
            }
        }
    }
}
