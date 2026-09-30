using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace dddlib.Generators;

/// <summary>
/// Reports mistakes in how an aggregate root applies and handles events, which the runtime does not report at all:
/// the event is recorded and nothing else happens.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EventApplicationAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        DiagnosticDescriptors.AppliedEventWithoutHandler,
        DiagnosticDescriptors.EventHandlerAppliesEvent,
        DiagnosticDescriptors.EventHandlerThrows,
        DiagnosticDescriptors.PropertySavedButNotLoaded,
        DiagnosticDescriptors.CannotBeLoaded);

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

            // Event and memento types are found where they are used, from several places at once; each is checked once.
            var serialized = new ConcurrentDictionary<ITypeSymbol, bool>(SymbolEqualityComparer.Default);

            startContext.RegisterOperationAction(operationContext => AnalyzeInvocation(operationContext, known, serialized), OperationKind.Invocation);
            startContext.RegisterOperationAction(operationContext => AnalyzeThrow(operationContext, known), OperationKind.Throw);
            startContext.RegisterOperationAction(operationContext => AnalyzeReturn(operationContext, known, serialized), OperationKind.Return);
            startContext.RegisterSymbolAction(symbolContext => AnalyzeMethod(symbolContext, known, serialized), SymbolKind.Method);
        });
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context, KnownSymbols known, ConcurrentDictionary<ITypeSymbol, bool> serialized)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (!SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.OriginalDefinition, known.Apply) || invocation.Arguments.Length != 1)
        {
            return;
        }

        if (GetContainingHandler(context.ContainingSymbol, known) is { } handler)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.EventHandlerAppliesEvent,
                invocation.Syntax.GetLocation(),
                handler.Name,
                handler.ContainingType.ToDisplayString()));
        }

        var argument = Unwrap(invocation.Arguments[0].Value);
        if (argument.Type is not null)
        {
            AnalyzeSerialized(argument.Type, "event", context.Compilation, serialized, context.ReportDiagnostic);
        }

        // Only a concrete class can be said to have no handler; anything else is not known until runtime.
        if (argument.Type is not INamedTypeSymbol { TypeKind: TypeKind.Class, IsAbstract: false, SpecialType: not SpecialType.System_Object } eventType ||
            (invocation.Instance?.Type ?? context.ContainingSymbol.ContainingType) is not INamedTypeSymbol aggregateRootType)
        {
            return;
        }

        if (!MayBeHandled(aggregateRootType, eventType, isExactType: argument is IObjectCreationOperation, known))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.AppliedEventWithoutHandler,
                invocation.Syntax.GetLocation(),
                eventType.ToDisplayString(),
                aggregateRootType.ToDisplayString()));
        }
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context, KnownSymbols known, ConcurrentDictionary<ITypeSymbol, bool> serialized)
    {
        var method = (IMethodSymbol)context.Symbol;
        if (method.IsDispatchableHandler() && known.GetDomainTypeKind(method.ContainingType) == DomainTypeKind.AggregateRoot)
        {
            AnalyzeSerialized(method.Parameters[0].Type, "event", context.Compilation, serialized, context.ReportDiagnostic);
        }
    }

    private static void AnalyzeReturn(OperationAnalysisContext context, KnownSymbols known, ConcurrentDictionary<ITypeSymbol, bool> serialized)
    {
        // The memento type is whatever an override of GetState creates and returns.
        if (((IReturnOperation)context.Operation).ReturnedValue is { } returned &&
            Unwrap(returned) is IObjectCreationOperation { Type: { } mementoType } &&
            context.ContainingSymbol is IMethodSymbol method &&
            method.Overrides(known.GetState))
        {
            AnalyzeSerialized(mementoType, "memento", context.Compilation, serialized, context.ReportDiagnostic);
        }
    }

    private static void AnalyzeSerialized(ITypeSymbol type, string kind, Compilation compilation, ConcurrentDictionary<ITypeSymbol, bool> serialized, Action<Diagnostic> report)
    {
        if (type is not INamedTypeSymbol { TypeKind: TypeKind.Class } serializedType ||
            !SymbolEqualityComparer.Default.Equals(serializedType.ContainingAssembly, compilation.Assembly) ||
            !serialized.TryAdd(serializedType, true))
        {
            return;
        }

        // An abstract class is never the runtime type of what is saved, so how it is constructed says nothing.
        if (serializedType.IsAbstract)
        {
            return;
        }

        var serializability = serializedType.IsDefaultSerializable();

        foreach (var property in serializability.UnloadedProperties)
        {
            if (property.Locations.FirstOrDefault(static location => location.IsInSource) is { } location)
            {
                report(Diagnostic.Create(DiagnosticDescriptors.PropertySavedButNotLoaded, location, property.Name, kind, serializedType.ToDisplayString()));
            }
        }

        // Without unloaded properties, what is left is the constructor: none the serializer can use, or one it cannot call.
        if (!serializability.IsSerializable &&
            serializability.UnloadedProperties.IsEmpty &&
            serializedType.Locations.FirstOrDefault(static location => location.IsInSource) is { } typeLocation)
        {
            var problem = serializability.UnboundParameter is { } parameter
                ? $"the constructor parameter '{parameter.Name}' has no public property of the same name"
                : "it has no public parameterless constructor, no single public constructor and no constructor marked [JsonConstructor]";

            report(Diagnostic.Create(DiagnosticDescriptors.CannotBeLoaded, typeLocation, kind, serializedType.ToDisplayString(), problem));
        }
    }

    private static IOperation Unwrap(IOperation operation)
    {
        while (operation is IConversionOperation { IsImplicit: true } conversion)
        {
            operation = conversion.Operand;
        }

        return operation;
    }

    private static void AnalyzeThrow(OperationAnalysisContext context, KnownSymbols known)
    {
        if (GetContainingHandler(context.ContainingSymbol, known) is { } handler)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.EventHandlerThrows,
                context.Operation.Syntax.GetLocation(),
                handler.Name,
                handler.ContainingType.ToDisplayString()));
        }
    }

    /// <summary>
    /// Gets the event handler the code belongs to, looking out of any lambdas and local functions, if it is one the
    /// runtime dispatches to.
    /// </summary>
    private static IMethodSymbol? GetContainingHandler(ISymbol symbol, KnownSymbols known)
    {
        while (symbol is IMethodSymbol { MethodKind: MethodKind.AnonymousFunction or MethodKind.LocalFunction })
        {
            symbol = symbol.ContainingSymbol;
        }

        return symbol is IMethodSymbol method && method.IsDispatchableHandler() && known.GetDomainTypeKind(method.ContainingType) == DomainTypeKind.AggregateRoot
            ? method
            : null;
    }

    private static bool MayBeHandled(INamedTypeSymbol aggregateRootType, INamedTypeSymbol eventType, bool isExactType, KnownSymbols known)
    {
        var hasHiddenHandlers = false;

        for (var current = aggregateRootType; current is not null && !SymbolEqualityComparer.Default.Equals(current, known.AggregateRoot); current = current.BaseType)
        {
            // The private members of a type in a referenced assembly are not imported, and handlers are usually private.
            hasHiddenHandlers |= current.Locations.All(static location => !location.IsInSource);

            foreach (var handler in current.GetDispatchableHandlers())
            {
                var handledType = handler.Parameters[0].Type;
                if (SymbolEqualityComparer.Default.Equals(handledType, eventType) || (!isExactType && handledType.IsOrDerivesFrom(eventType)))
                {
                    return true;
                }
            }
        }

        return hasHiddenHandlers;
    }
}
