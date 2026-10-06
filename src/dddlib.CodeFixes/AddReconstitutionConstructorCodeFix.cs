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
/// DDDLIB014: adds the parameterless constructor the runtime reconstitutes an aggregate root with, after the last
/// constructor of the type.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddReconstitutionConstructorCodeFix))]
[Shared]
public sealed class AddReconstitutionConstructorCodeFix : CodeFixProvider
{
    private const string Title = "Add a constructor for reconstitution";

    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DiagnosticDescriptors.NoReconstitutionFactory.Id);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || SyntaxHelpers.FindTypeDeclaration(root, context.Diagnostics[0]) is not { } type)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(Title, _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(type, AddConstructor(type)))), equivalenceKey: Title),
            context.Diagnostics);
    }

    private static TypeDeclarationSyntax AddConstructor(TypeDeclarationSyntax type)
    {
        // protected so that a derived aggregate root can chain to it; a protected member of a sealed type is a
        // compiler warning, and DDDLIB025 reports the sealed type itself
        var accessibility = type.Modifiers.Any(SyntaxKind.SealedKeyword) ? "private" : "protected";

        var lastConstructor = type.Members.LastOrDefault(static member => member is ConstructorDeclarationSyntax constructor && !constructor.Modifiers.Any(SyntaxKind.StaticKeyword));
        var index = lastConstructor is null ? 0 : type.Members.IndexOf(lastConstructor) + 1;

        return SyntaxHelpers.InsertMember(
            type,
            index,
            "// used for reconstitution only",
            $"{accessibility} {type.Identifier.Text}()",
            "{",
            "}");
    }
}
