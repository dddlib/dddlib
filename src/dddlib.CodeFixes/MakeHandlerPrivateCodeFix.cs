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
/// DDDLIB003: makes a public event handler private, so the dispatcher calls it.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MakeHandlerPrivateCodeFix))]
[Shared]
public sealed class MakeHandlerPrivateCodeFix : CodeFixProvider
{
    private const string Title = "Make the event handler private";

    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DiagnosticDescriptors.PublicEventHandler.Id);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var method = root?.FindNode(context.Diagnostics[0].Location.SourceSpan).FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (root is null || method is null)
        {
            return;
        }

        var @public = method.Modifiers.FirstOrDefault(static modifier => modifier.IsKind(SyntaxKind.PublicKeyword));
        if (!@public.IsKind(SyntaxKind.PublicKeyword))
        {
            return;
        }

        var @private = SyntaxFactory.Token(SyntaxKind.PrivateKeyword).WithTriviaFrom(@public);

        context.RegisterCodeFix(
            CodeAction.Create(Title, _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceToken(@public, @private))), equivalenceKey: Title),
            context.Diagnostics);
    }
}
