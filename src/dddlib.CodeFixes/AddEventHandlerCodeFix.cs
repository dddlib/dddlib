using System.Collections.Immutable;
using System.Composition;
using dddlib.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace dddlib.CodeFixes;

/// <summary>
/// DDDLIB008: adds an empty private handler for the applied event, after the last handler of the type that applies it.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddEventHandlerCodeFix))]
[Shared]
public sealed class AddEventHandlerCodeFix : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DiagnosticDescriptors.AppliedEventWithoutHandler.Id);

    // Two unhandled Apply calls for the same event would each add the handler.
    public override FixAllProvider? GetFixAllProvider() => null;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        var invocation = root?.FindNode(context.Diagnostics[0].Location.SourceSpan, getInnermostNodeForTie: true).FirstAncestorOrSelf<InvocationExpressionSyntax>();
        var type = invocation?.FirstAncestorOrSelf<TypeDeclarationSyntax>();

        if (root is null || semanticModel is null || invocation is null || type is null ||
            semanticModel.GetOperation(invocation, context.CancellationToken) is not IInvocationOperation { Arguments.Length: 1 } operation)
        {
            return;
        }

        var argument = operation.Arguments[0].Value;
        while (argument is IConversionOperation { IsImplicit: true } conversion)
        {
            argument = conversion.Operand;
        }

        if (argument.Type is not { } eventType)
        {
            return;
        }

        var title = $"Add a handler for '{eventType.Name}'";
        var eventTypeName = eventType.ToMinimalDisplayString(semanticModel, type.OpenBraceToken.SpanStart);

        context.RegisterCodeFix(
            CodeAction.Create(title, _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(type, AddHandler(type, eventTypeName)))), equivalenceKey: title),
            context.Diagnostics);
    }

    private static TypeDeclarationSyntax AddHandler(TypeDeclarationSyntax type, string eventTypeName)
    {
        var lastHandler = type.Members.LastOrDefault(static member =>
            member is MethodDeclarationSyntax { ParameterList.Parameters.Count: 1 } method &&
            string.Equals(method.Identifier.ValueText, "Handle", StringComparison.OrdinalIgnoreCase));

        return SyntaxHelpers.InsertMember(
            type,
            lastHandler is null ? type.Members.Count : type.Members.IndexOf(lastHandler) + 1,
            $"private void Handle({eventTypeName} @event)",
            "{",
            "}");
    }
}
