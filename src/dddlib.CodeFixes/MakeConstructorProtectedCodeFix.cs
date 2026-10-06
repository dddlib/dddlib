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
/// DDDLIB027: makes the reconstitution constructor of an aggregate root protected, so that a derived aggregate root
/// can chain to it.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MakeConstructorProtectedCodeFix))]
[Shared]
public sealed class MakeConstructorProtectedCodeFix : CodeFixProvider
{
    private const string Title = "Make the constructor protected";

    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DiagnosticDescriptors.InaccessibleReconstitutionConstructor.Id);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var constructor = root?.FindNode(context.Diagnostics[0].Location.SourceSpan).FirstAncestorOrSelf<ConstructorDeclarationSyntax>();
        if (root is null || constructor is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(Title, _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(constructor, MakeProtected(constructor)))), equivalenceKey: Title),
            context.Diagnostics);
    }

    private static ConstructorDeclarationSyntax MakeProtected(ConstructorDeclarationSyntax constructor)
    {
        // 'private' or 'private protected' becomes 'protected', in place of the first of them
        var accessibility = constructor.Modifiers
            .Where(static modifier => modifier.IsKind(SyntaxKind.PrivateKeyword) || modifier.IsKind(SyntaxKind.ProtectedKeyword))
            .ToList();
        var index = constructor.Modifiers.IndexOf(accessibility[0]);
        var @protected = SyntaxFactory.Token(SyntaxKind.ProtectedKeyword)
            .WithLeadingTrivia(accessibility[0].LeadingTrivia)
            .WithTrailingTrivia(accessibility[accessibility.Count - 1].TrailingTrivia);

        var modifiers = SyntaxFactory.TokenList(constructor.Modifiers.Where(static modifier => !modifier.IsKind(SyntaxKind.PrivateKeyword) && !modifier.IsKind(SyntaxKind.ProtectedKeyword)));

        return constructor.WithModifiers(modifiers.Insert(index, @protected));
    }
}
