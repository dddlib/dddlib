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
/// DDDLIB025: removes <c>sealed</c> from an entity or an aggregate root, so that it can be derived from.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UnsealTypeCodeFix))]
[Shared]
public sealed class UnsealTypeCodeFix : CodeFixProvider
{
    private const string Title = "Remove 'sealed'";

    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DiagnosticDescriptors.SealedEntity.Id);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null ||
            SyntaxHelpers.FindTypeDeclaration(root, context.Diagnostics[0]) is not { } type ||
            !type.Modifiers.Any(SyntaxKind.SealedKeyword))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(Title, _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(type, Unseal(type)))), equivalenceKey: Title),
            context.Diagnostics);
    }

    private static TypeDeclarationSyntax Unseal(TypeDeclarationSyntax type)
    {
        var @sealed = type.Modifiers.First(static modifier => modifier.IsKind(SyntaxKind.SealedKeyword));
        var index = type.Modifiers.IndexOf(@sealed);
        var modifiers = type.Modifiers.RemoveAt(index);

        // the token that follows 'sealed' takes its leading trivia, such as the indentation of the declaration
        if (index < modifiers.Count)
        {
            return type.WithModifiers(modifiers.Replace(modifiers[index], modifiers[index].WithLeadingTrivia(@sealed.LeadingTrivia)));
        }

        return type.WithModifiers(modifiers).WithKeyword(type.Keyword.WithLeadingTrivia(@sealed.LeadingTrivia));
    }
}
