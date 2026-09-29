using BenchmarkDotNet.Attributes;

namespace dddlib.Benchmarks;

// Entity equality, which reads the natural key through the compiled-expression accessor (non-partial type) or the
// generated accessor (partial type).
[MemoryDiagnoser]
public class NaturalKeyBenchmarks
{
    private readonly ReflectionEntity reflectionLeft = new("key");
    private readonly ReflectionEntity reflectionRight = new("key");
    private readonly GeneratedEntity generatedLeft = new("key");
    private readonly GeneratedEntity generatedRight = new("key");

    [Benchmark(Baseline = true)]
    public bool EqualsViaReflection() => this.reflectionLeft.Equals(this.reflectionRight);

    [Benchmark]
    public bool EqualsViaGeneratedAccessor() => this.generatedLeft.Equals(this.generatedRight);

    public class ReflectionEntity : Entity
    {
        public ReflectionEntity(string key) => this.Key = key;

        [NaturalKey]
        public string Key { get; }
    }

    public partial class GeneratedEntity : Entity
    {
        public GeneratedEntity(string key) => this.Key = key;

        [NaturalKey]
        public string Key { get; }
    }
}
