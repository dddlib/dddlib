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
/// DDDLIB007: makes a domain type and every type that contains it partial, so dddlib can generate for it. A type
/// that is not partial has a single declaration, which contains the declarations of everything nested in it, so the
/// change never leaves the document.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MakePartialCodeFix))]
[Shared]
public sealed class MakePartialCodeFix : CodeFixProvider
{
    private const string Title = "Make the type and its containing types partial";

    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DiagnosticDescriptors.TypeShouldBePartial.Id);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || SyntaxHelpers.FindTypeDeclaration(root, context.Diagnostics[0]) is not { } type)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(Title, _ => Task.FromResult(context.Document.WithSyntaxRoot(MakePartial(root, type))), equivalenceKey: Title),
            context.Diagnostics);
    }

    private static SyntaxNode MakePartial(SyntaxNode root, TypeDeclarationSyntax type)
    {
        var declarations = type.AncestorsAndSelf()
            .OfType<TypeDeclarationSyntax>()
            .Where(static declaration => !declaration.Modifiers.Any(SyntaxKind.PartialKeyword));

        return root.ReplaceNodes(declarations, static (_, declaration) => AddPartial(declaration));
    }

    private static TypeDeclarationSyntax AddPartial(TypeDeclarationSyntax type)
    {
        // partial goes last, directly before the keyword
        var partial = SyntaxFactory.Token(SyntaxKind.PartialKeyword).WithTrailingTrivia(SyntaxFactory.Space);

        if (type.Modifiers.Count > 0)
        {
            return type.WithModifiers(type.Modifiers.Add(partial));
        }

        // without modifiers the keyword is what carries the indentation and any comments, which now belong to partial
        return type
            .WithKeyword(type.Keyword.WithLeadingTrivia())
            .WithModifiers(SyntaxFactory.TokenList(partial.WithLeadingTrivia(type.Keyword.LeadingTrivia)));
    }
}
