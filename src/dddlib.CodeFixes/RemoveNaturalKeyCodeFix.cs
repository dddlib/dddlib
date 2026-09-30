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
/// DDDLIB001: removes one of the <c>[NaturalKey]</c> attributes of an entity that has several. One fix is offered
/// per attribute; which property is the natural key is the author's decision.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RemoveNaturalKeyCodeFix))]
[Shared]
public sealed class RemoveNaturalKeyCodeFix : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DiagnosticDescriptors.MultipleNaturalKeys.Id);

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

        var solution = context.Document.Project.Solution;

        foreach (var property in known.GetDeclaredNaturalKeyProperties(symbol).OrderBy(static property => property.Locations.FirstOrDefault()?.SourceSpan.Start ?? 0))
        {
            var reference = property.GetAttributes()
                .First(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, known.NaturalKeyAttribute))
                .ApplicationSyntaxReference;

            // the property may be in another part of a partial type, and so in another document
            if (reference is null || solution.GetDocument(reference.SyntaxTree) is not { } document)
            {
                continue;
            }

            var title = $"Remove [NaturalKey] from '{property.Name}'";

            context.RegisterCodeFix(
                CodeAction.Create(title, cancellationToken => RemoveAsync(document, reference, cancellationToken), equivalenceKey: title),
                context.Diagnostics);
        }
    }

    private static async Task<Document> RemoveAsync(Document document, SyntaxReference reference, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null ||
            await reference.GetSyntaxAsync(cancellationToken).ConfigureAwait(false) is not AttributeSyntax { Parent: AttributeListSyntax { Parent: MemberDeclarationSyntax member } list } attribute)
        {
            return document;
        }

        if (list.Attributes.Count > 1)
        {
            return document.WithSyntaxRoot(root.ReplaceNode(list, list.WithAttributes(list.Attributes.Remove(attribute))));
        }

        // The list goes. When it was the first thing in the declaration it carried the blank line and indentation
        // that precede the member, which the member keeps.
        var isFirst = member.AttributeLists[0] == list;
        var changed = member.WithAttributeLists(member.AttributeLists.Remove(list));

        return document.WithSyntaxRoot(root.ReplaceNode(member, isFirst ? changed.WithLeadingTrivia(list.GetLeadingTrivia()) : changed));
    }
}
