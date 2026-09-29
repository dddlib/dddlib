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

    public static CSharpCompilation Create(string source, string assemblyName = "TestAssembly") =>
        CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest))],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

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

    public static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source, params DiagnosticAnalyzer[] analyzers) =>
        await Create(source).WithAnalyzers([.. analyzers]).GetAnalyzerDiagnosticsAsync();

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
