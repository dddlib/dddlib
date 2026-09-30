using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Simplification;

namespace dddlib.CodeFixes;

/// <summary>
/// Adds a member to a type the way a person would: indented like its neighbours, with the file's line endings, and
/// separated from them by a blank line. The formatter is not used, so nothing else in the file moves.
/// </summary>
internal static class SyntaxHelpers
{
    /// <summary>
    /// Gets the type declaration the diagnostic was reported in.
    /// </summary>
    public static TypeDeclarationSyntax? FindTypeDeclaration(SyntaxNode root, Diagnostic diagnostic) =>
        root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true).FirstAncestorOrSelf<TypeDeclarationSyntax>();

    /// <summary>
    /// Inserts a member written as lines of source, without indentation or line endings, at the specified position
    /// among the members of the type.
    /// </summary>
    public static TypeDeclarationSyntax InsertMember(TypeDeclarationSyntax type, int index, params string[] lines)
    {
        var endOfLine = GetEndOfLine(type);
        var indentation = GetMemberIndentation(type);
        var blankLine = SyntaxFactory.EndOfLine(endOfLine);

        var text = string.Concat(lines.Select(line => line.Length == 0 ? endOfLine : indentation + line + endOfLine));
        var member = SyntaxFactory.ParseMemberDeclaration(text)!.WithAdditionalAnnotations(Simplifier.Annotation);

        var members = type.Members;
        if (index > 0)
        {
            member = member.WithLeadingTrivia(member.GetLeadingTrivia().Insert(0, blankLine));
        }

        if (index < members.Count && !StartsWithBlankLine(members[index]))
        {
            members = members.Replace(members[index], members[index].WithLeadingTrivia(members[index].GetLeadingTrivia().Insert(0, blankLine)));
        }

        return type.WithMembers(members.Insert(index, member));
    }

    private static string GetEndOfLine(SyntaxNode node)
    {
        var endOfLine = node.SyntaxTree.GetRoot().DescendantTrivia().FirstOrDefault(static trivia => trivia.IsKind(SyntaxKind.EndOfLineTrivia));

        return endOfLine.IsKind(SyntaxKind.EndOfLineTrivia) ? endOfLine.ToString() : "\n";
    }

    private static string GetMemberIndentation(TypeDeclarationSyntax type) =>
        type.Members.Count > 0 ? GetIndentation(type.Members[0]) : GetIndentation(type) + "    ";

    private static string GetIndentation(SyntaxNode node)
    {
        var leading = node.GetLeadingTrivia();

        return leading.Count > 0 && leading[leading.Count - 1].IsKind(SyntaxKind.WhitespaceTrivia) ? leading[leading.Count - 1].ToString() : string.Empty;
    }

    private static bool StartsWithBlankLine(SyntaxNode node) =>
        node.GetLeadingTrivia().SkipWhile(static trivia => trivia.IsKind(SyntaxKind.WhitespaceTrivia)).FirstOrDefault().IsKind(SyntaxKind.EndOfLineTrivia);
}
