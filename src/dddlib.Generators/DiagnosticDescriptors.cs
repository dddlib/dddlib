using Microsoft.CodeAnalysis;

namespace dddlib.Generators;

internal static class DiagnosticDescriptors
{
    private const string Category = "dddlib";
    private const string WikiEntityEquality = "https://github.com/dddlib/dddlib/blob/main/docs/entity-equality.md";
    private const string WikiEventApplication = "https://github.com/dddlib/dddlib/blob/main/docs/aggregate-root-event-application.md";
    private const string WikiValueObjectEquality = "https://github.com/dddlib/dddlib/blob/main/docs/value-object-equality.md";
    private const string WikiBootstrapper = "https://github.com/dddlib/dddlib/blob/main/docs/bootstrapper.md";
    private const string WikiAggregateRootEquality = "https://github.com/dddlib/dddlib/blob/main/docs/aggregate-root-equality.md";
    private const string WikiReconstitution = "https://github.com/dddlib/dddlib/blob/main/docs/aggregate-root-reconstitution.md";
    private const string WikiMementos = "https://github.com/dddlib/dddlib/blob/main/docs/aggregate-root-mementos.md";
    private const string WikiValueObjectSerialization = "https://github.com/dddlib/dddlib/blob/main/docs/value-object-serialization.md";
    private const string WikiValueObjects = "https://github.com/dddlib/dddlib/blob/main/docs/value-objects.md";
    private const string WikiSerialization = "https://github.com/dddlib/dddlib/blob/main/docs/persistence/serialization.md";

    public static readonly DiagnosticDescriptor MultipleNaturalKeys = new(
        "DDDLIB001",
        "Entity has more than one natural key",
        "Entity of type '{0}' has more than one natural key defined",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Ensure that there is only a single natural key defined for the entity.",
        helpLinkUri: WikiEntityEquality);

    public static readonly DiagnosticDescriptor ValueTypeEventHandler = new(
        "DDDLIB002",
        "Event handler takes a value type",
        "The event handler '{0}' on '{1}' takes the value type '{2}' and will never be called; events must be classes",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The event dispatcher only dispatches to handlers whose parameter is a class.",
        helpLinkUri: WikiEventApplication);

    public static readonly DiagnosticDescriptor PublicEventHandler = new(
        "DDDLIB003",
        "Event handler is public",
        "The event handler '{0}' on '{1}' is public and will not be dispatched to; make it private",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The event dispatcher only dispatches to non-public handlers.",
        helpLinkUri: WikiEventApplication);

    public static readonly DiagnosticDescriptor ValueObjectWithoutProperties = new(
        "DDDLIB004",
        "Value object has no public properties",
        "The value object of type '{0}' does not have any public properties, so under the default equality comparer no two instances will ever be equal",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Add one or more public properties to the value object, or define a custom value object equality comparer in a bootstrapper.",
        helpLinkUri: WikiValueObjectEquality);

    public static readonly DiagnosticDescriptor MultipleBootstrappers = new(
        "DDDLIB005",
        "Assembly has more than one bootstrapper",
        "The assembly '{0}' has more than one bootstrapper defined; there can only be a single bootstrapper defined per assembly",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Ensure that there is only a single instance of a bootstrapper class declared in the assembly.",
        helpLinkUri: WikiBootstrapper,
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    public static readonly DiagnosticDescriptor BootstrapperWithoutDefaultConstructor = new(
        "DDDLIB006",
        "Bootstrapper has no default constructor",
        "The bootstrapper of type '{0}' cannot be instantiated as it does not have a public default constructor",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Add a public default constructor to the bootstrapper.",
        helpLinkUri: WikiBootstrapper);

    public static readonly DiagnosticDescriptor MappingNotConfigured = new(
        "DDDLIB020",
        "Mapping is not configured",
        "No mapping from '{0}' to '{1}' is configured, so this throws at runtime; call {2} in the bootstrapper",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Map converts between entities or value objects and events with the mappings that the bootstrapper configures through ToMapToEvent. Mapping an event back needs the overload that takes a reverse mapping.",
        helpLinkUri: WikiBootstrapper);

    public static readonly DiagnosticDescriptor MappingDoesNotFit = new(
        "DDDLIB024",
        "Mapping does not fit how it is used",
        "The mapping from '{0}' to '{1}' {2}, so this throws at runtime; call ToMapToEvent<{3}> with a mapping that {4} in the bootstrapper",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A mapping configured through ToMapToEvent either creates the event or is given it. ToEvent<T>() uses the first, or the second with an event it creates with its public parameterless constructor. ToEvent(@event) uses the second.",
        helpLinkUri: WikiBootstrapper);

    public static readonly DiagnosticDescriptor ConflictingNaturalKeySelector = new(
        "DDDLIB021",
        "Bootstrapper selects a different natural key",
        "The bootstrapper selects '{0}' as the natural key of '{1}', which declares '{2}' as its natural key with [NaturalKey]",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "An entity has a single natural key. Remove the attribute or the selector, or make them agree.",
        helpLinkUri: WikiEntityEquality);

    public static readonly DiagnosticDescriptor InvalidNaturalKeySelector = new(
        "DDDLIB022",
        "Natural key selector is not a property of the entity",
        "The natural key selector must return a property of its parameter, such as 'entity => entity.Id'",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "ToUseNaturalKey reads the name of the selected property from the expression and rejects any other expression when the bootstrapper runs.",
        helpLinkUri: WikiEntityEquality);

    public static readonly DiagnosticDescriptor TypeShouldBePartial = new(
        "DDDLIB007",
        "Domain type could be partial",
        "Make '{0}' and its containing types partial to let dddlib generate its event dispatch and metadata instead of using reflection",
        Category,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "dddlib generates event dispatch, natural key access, reconstitution and value object equality for partial types.");

    public static readonly DiagnosticDescriptor AppliedEventWithoutHandler = new(
        "DDDLIB008",
        "Applied event has no handler",
        "The event of type '{0}' applied by '{1}' has no handler, so applying it changes no state; add a private 'Handle' method that takes it",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "An event is dispatched to the non-public 'Handle' method whose parameter type is exactly the type of the event. An event without one is recorded but changes nothing.",
        helpLinkUri: WikiEventApplication);

    public static readonly DiagnosticDescriptor AbstractEventHandler = new(
        "DDDLIB009",
        "Event handler takes an abstract class or an interface",
        "The event handler '{0}' on '{1}' takes the {2} '{3}' and will never be called; an event is dispatched to the handler for exactly its own type",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The event dispatcher matches the runtime type of an event exactly, and no event has an abstract class or an interface as its runtime type.",
        helpLinkUri: WikiEventApplication);

    public static readonly DiagnosticDescriptor EventHandlerAppliesEvent = new(
        "DDDLIB010",
        "Event handler applies an event",
        "The event handler '{0}' on '{1}' applies an event; handlers also run when the aggregate root is loaded, where the event would be recorded again",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "An event handler only changes state. Apply every event from the method that makes the decision.",
        helpLinkUri: WikiEventApplication);

    public static readonly DiagnosticDescriptor EventHandlerThrows = new(
        "DDDLIB011",
        "Event handler throws",
        "The event handler '{0}' on '{1}' throws; handlers also run when the aggregate root is loaded, so validate before applying the event instead",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "An event handler only changes state. An event that was applied has happened, and replaying it must not fail.",
        helpLinkUri: WikiEventApplication);

    public static readonly DiagnosticDescriptor PropertySavedButNotLoaded = new(
        "DDDLIB012",
        "Property is saved but never loaded",
        "The property '{0}' of the {1} '{2}' is written when the {1} is saved but never read back; it has no public setter and no constructor parameter of the same name",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Events and mementos are serialized with System.Text.Json, which sets a property through its public setter or a constructor parameter of the same name.",
        helpLinkUri: WikiSerialization);

    public static readonly DiagnosticDescriptor CannotBeLoaded = new(
        "DDDLIB023",
        "Event or memento cannot be loaded",
        "The {0} '{1}' can be saved but not loaded, because {2}",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Events and mementos are serialized with System.Text.Json, which creates one with its public parameterless constructor, its only public constructor or the constructor marked [JsonConstructor], and needs a property for every parameter of that constructor. Saving does not need the constructor, so the failure only shows on load.",
        helpLinkUri: WikiSerialization);

    public static readonly DiagnosticDescriptor IncompleteMemento = new(
        "DDDLIB013",
        "Aggregate root overrides only one of GetState and SetState",
        "The aggregate root '{0}' overrides '{1}' but not '{2}', so its memento cannot be {3}",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "An aggregate root that is persisted as a memento produces it in GetState and consumes it in SetState. Override both or neither.",
        helpLinkUri: WikiMementos);

    public static readonly DiagnosticDescriptor NoReconstitutionFactory = new(
        "DDDLIB014",
        "Aggregate root cannot be reconstituted",
        "The aggregate root '{0}' has no parameterless constructor and no reconstitution factory, so it cannot be persisted and the events it applies are not recorded; add a parameterless constructor, which need not be public, or call ToReconstituteUsing in the bootstrapper",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Reconstituting an aggregate root requires an uninitialized instance to apply the saved state to. The runtime creates it with the parameterless constructor, whatever its accessibility, or with the factory configured in the bootstrapper.",
        helpLinkUri: WikiReconstitution);

    public static readonly DiagnosticDescriptor NoNaturalKey = new(
        "DDDLIB015",
        "Aggregate root has no natural key",
        "The aggregate root '{0}' has no natural key, so it cannot be persisted; mark a property with [NaturalKey] or call ToUseNaturalKey in the bootstrapper",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "An aggregate root is saved and loaded by its natural key.",
        helpLinkUri: WikiAggregateRootEquality);

    public static readonly DiagnosticDescriptor IgnoredNaturalKey = new(
        "DDDLIB016",
        "Natural key attribute has no effect",
        "The [NaturalKey] on '{0}' is ignored because {1}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A natural key is a public instance property with a getter, declared on an entity or an aggregate root.",
        helpLinkUri: WikiEntityEquality);

    public static readonly DiagnosticDescriptor NaturalKeyDoesNotRoundTrip = new(
        "DDDLIB017",
        "Natural key does not round-trip",
        "The natural key '{0}' has the type '{1}', which {2}",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The natural key of an aggregate root is serialized when it is saved, and the deserialized key must equal the original. Use a string, a value type, or a value object that its serializer can read back.",
        helpLinkUri: WikiValueObjectSerialization);

    public static readonly DiagnosticDescriptor ValueObjectOfAnotherType = new(
        "DDDLIB018",
        "Value object does not derive from ValueObject of itself",
        "The value object '{0}' derives from 'ValueObject<{1}>'; it must derive from 'ValueObject<{0}>'",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The type argument of ValueObject<T> is the value object itself. Equality, serialization and configuration are all keyed on it.",
        helpLinkUri: WikiValueObjects);

    public static readonly DiagnosticDescriptor ValueObjectPropertyComparedByReference = new(
        "DDDLIB019",
        "Value object property is compared by reference",
        "The property '{0}' of the value object '{1}' has the type '{2}', which is compared by reference, so two value objects with the same content are not equal; use a value object or another type with value equality, or configure an equality comparer in the bootstrapper",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The default equality comparer of a value object compares each public property with the equality of the property's type, and collections element by element.",
        helpLinkUri: WikiValueObjectEquality);
}
