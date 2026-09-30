using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace dddlib.CodeFixes.Tests;

/// <summary>
/// Runs an analyzer over a source in a workspace, applies one of the code actions a provider offers for the first
/// diagnostic with the given id, and returns the resulting source.
/// </summary>
internal static class CodeFixVerifier
{
    private static readonly MetadataReference[] References = BuildReferences();

    public static async Task<string> ApplyAsync(string source, DiagnosticAnalyzer analyzer, CodeFixProvider provider, string diagnosticId, int action = 0)
    {
        var (document, actions) = await GetActionsAsync(source, analyzer, provider, diagnosticId);

        var operations = await actions[action].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
        var text = await changed.GetDocument(document.Id)!.GetTextAsync();

        return Normalize(text.ToString());
    }

    public static async Task<IReadOnlyList<string>> GetTitlesAsync(string source, DiagnosticAnalyzer analyzer, CodeFixProvider provider, string diagnosticId)
    {
        var (_, actions) = await GetActionsAsync(source, analyzer, provider, diagnosticId);

        return [.. actions.Select(static action => action.Title)];
    }

    public static string Normalize(string text) => text.ReplaceLineEndings("\n");

    private static async Task<(Document Document, List<CodeAction> Actions)> GetActionsAsync(string source, DiagnosticAnalyzer analyzer, CodeFixProvider provider, string diagnosticId)
    {
        // The workspace is not disposed: the documents it produced are read after this method returns, and it holds nothing but memory.
        var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Default,
            "TestAssembly",
            "TestAssembly",
            LanguageNames.CSharp,
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable),
            parseOptions: new CSharpParseOptions(LanguageVersion.Latest),
            metadataReferences: References));
        var document = workspace.AddDocument(project.Id, "Test.cs", SourceText.From(source));

        var compilation = (await document.Project.GetCompilationAsync())!;

        // a source that does not compile proves nothing about the code fix
        var errors = compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToList();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        var diagnostic = (await compilation.WithAnalyzers([analyzer]).GetAnalyzerDiagnosticsAsync())
            .Where(candidate => candidate.Id == diagnosticId)
            .OrderBy(static candidate => candidate.Location.SourceSpan.Start)
            .First();

        if (!provider.FixableDiagnosticIds.Contains(diagnosticId))
        {
            throw new InvalidOperationException($"The provider does not fix {diagnosticId}.");
        }

        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));

        return (document, actions);
    }

    private static MetadataReference[] BuildReferences()
    {
        var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(static path => Path.GetFileName(path) is var name && (name.StartsWith("System.", StringComparison.Ordinal) || name is "netstandard.dll" or "mscorlib.dll" or "Microsoft.CSharp.dll"))
            .Select(static path => MetadataReference.CreateFromFile(path));

        return [.. trustedAssemblies, MetadataReference.CreateFromFile(typeof(AggregateRoot).Assembly.Location)];
    }
}
