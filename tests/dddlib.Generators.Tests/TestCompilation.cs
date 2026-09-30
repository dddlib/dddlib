using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace dddlib.Generators.Tests;

/// <summary>
/// Compiles test sources against the running process's framework assemblies and dddlib, then runs generators or
/// analyzers over them.
/// </summary>
internal static class TestCompilation
{
    private static readonly MetadataReference[] References = BuildReferences();

    public static CSharpCompilation Create(string source, string assemblyName = "TestAssembly", params MetadataReference[] references) =>
        CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest))],
            [.. References, .. references],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

    /// <summary>
    /// Compiles the source into an assembly image and returns a reference to it, so that a test sees its types as
    /// metadata, the way a consumer sees a referenced package.
    /// </summary>
    public static MetadataReference Emit(string source, string assemblyName)
    {
        using var image = new MemoryStream();
        var result = Create(source, assemblyName).Emit(image);
        if (!result.Success)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics));
        }

        return MetadataReference.CreateFromImage(image.ToArray());
    }

    public static GeneratorRun RunGenerators(string source, params IIncrementalGenerator[] generators)
    {
        var compilation = Create(source);
        var driver = CSharpGeneratorDriver.Create(generators.Select(static generator => generator.AsSourceGenerator()).ToArray());
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);

        var sources = driver.GetRunResult().Results
            .SelectMany(static result => result.GeneratedSources)
            .ToDictionary(static generated => generated.HintName, static generated => generated.SourceText.ToString(), StringComparer.Ordinal);

        return new GeneratorRun(output, sources);
    }

    public static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source, params DiagnosticAnalyzer[] analyzers) =>
        AnalyzeAsync(Create(source), analyzers);

    public static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(CSharpCompilation compilation, params DiagnosticAnalyzer[] analyzers)
    {
        // a source that does not compile proves nothing about the analyzers
        var errors = compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToList();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        return await compilation.WithAnalyzers([.. analyzers]).GetAnalyzerDiagnosticsAsync();
    }

    private static MetadataReference[] BuildReferences()
    {
        var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(static path => Path.GetFileName(path) is var name && (name.StartsWith("System.", StringComparison.Ordinal) || name is "netstandard.dll" or "mscorlib.dll" or "Microsoft.CSharp.dll"))
            .Select(static path => MetadataReference.CreateFromFile(path));

        return [.. trustedAssemblies, MetadataReference.CreateFromFile(typeof(AggregateRoot).Assembly.Location)];
    }

    internal sealed record GeneratorRun(Compilation Output, IReadOnlyDictionary<string, string> Sources)
    {
        public IEnumerable<Diagnostic> Errors => this.Output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

        public string AllGeneratedSource => string.Join("\n", this.Sources.Values);
    }
}
