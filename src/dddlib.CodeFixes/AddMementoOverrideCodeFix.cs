using System.Collections.Immutable;
using System.Composition;
using dddlib.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace dddlib.CodeFixes;

/// <summary>
/// DDDLIB013: adds whichever of the <c>GetState</c> and <c>SetState</c> overrides is missing, as a stub to fill in.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddMementoOverrideCodeFix))]
[Shared]
public sealed class AddMementoOverrideCodeFix : CodeFixProvider
{
    private const string HelpLink = "https://github.com/dddlib/dddlib/blob/main/docs/aggregate-root-mementos.md";

    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DiagnosticDescriptors.IncompleteMemento.Id);

    public override FixAllProvider? GetFixAllProvider() => null;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

        if (root is null || semanticModel is null ||
            SyntaxHelpers.FindTypeDeclaration(root, context.Diagnostics[0]) is not { } type ||
            semanticModel.GetDeclaredSymbol(type, context.CancellationToken) is not { } symbol ||
            KnownSymbols.Create(semanticModel.Compilation) is not { } known)
        {
            return;
        }

        var addGetState = symbol.FindOverride(known.GetState) is null;
        if (addGetState == (symbol.FindOverride(known.SetState) is null))
        {
            return;
        }

        var title = addGetState ? "Override GetState" : "Override SetState";
        var nullable = semanticModel.GetNullableContext(type.SpanStart).AnnotationsEnabled() ? "?" : string.Empty;

        context.RegisterCodeFix(
            CodeAction.Create(title, _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(type, AddOverride(type, addGetState, nullable)))), equivalenceKey: title),
            context.Diagnostics);
    }

    private static TypeDeclarationSyntax AddOverride(TypeDeclarationSyntax type, bool addGetState, string nullable)
    {
        // next to the override the type already has, if it is declared here rather than inherited
        var existing = type.Members.LastOrDefault(member =>
            member is MethodDeclarationSyntax method &&
            method.Modifiers.Any(SyntaxKind.OverrideKeyword) &&
            method.Identifier.ValueText == (addGetState ? "SetState" : "GetState"));
        var index = existing is null ? type.Members.Count : type.Members.IndexOf(existing) + 1;

        return addGetState
            ? SyntaxHelpers.InsertMember(
                type,
                index,
                $"// TODO: return a memento of the state; see {HelpLink}",
                $"protected override object{nullable} GetState()",
                "{",
                "    return null;",
                "}")
            : SyntaxHelpers.InsertMember(
                type,
                index,
                $"// TODO: restore the state from the memento; see {HelpLink}",
                "protected override void SetState(object memento)",
                "{",
                "    throw new global::System.NotImplementedException();",
                "}");
    }
}
