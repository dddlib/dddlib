using Microsoft.CodeAnalysis;

namespace dddlib.Generators;

internal static class DiagnosticDescriptors
{
    private const string Category = "dddlib";
    private const string WikiEntityEquality = "https://github.com/dddlib/dddlib/blob/main/docs/entity-equality.md";
    private const string WikiEventApplication = "https://github.com/dddlib/dddlib/blob/main/docs/aggregate-root-event-application.md";
    private const string WikiValueObjectEquality = "https://github.com/dddlib/dddlib/blob/main/docs/value-object-equality.md";
    private const string WikiBootstrapper = "https://github.com/dddlib/dddlib/blob/main/docs/bootstrapper.md";

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

    public static readonly DiagnosticDescriptor TypeShouldBePartial = new(
        "DDDLIB007",
        "Domain type could be partial",
        "Make '{0}' and its containing types partial to let dddlib generate its event dispatch and metadata instead of using reflection",
        Category,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "dddlib generates event dispatch, natural key access, reconstitution and value object equality for partial types.");
}
