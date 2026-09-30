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
        DiagnosticDescriptors.AppliedEventWithoutHandler);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(static startContext =>
        {
            if (KnownSymbols.Create(startContext.Compilation) is { } known)
            {
                startContext.RegisterOperationAction(operationContext => AnalyzeInvocation(operationContext, known), OperationKind.Invocation);
            }
        });
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context, KnownSymbols known)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (!SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.OriginalDefinition, known.Apply) || invocation.Arguments.Length != 1)
        {
            return;
        }

        var argument = invocation.Arguments[0].Value;
        while (argument is IConversionOperation { IsImplicit: true } conversion)
        {
            argument = conversion.Operand;
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
