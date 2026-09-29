using BenchmarkDotNet.Attributes;

namespace dddlib.Benchmarks;

// Event application through the reflection-based dispatcher (non-partial type) and the generated one (partial type).
// The aggregates have no parameterless constructor, so applied events are dispatched but not recorded.
[MemoryDiagnoser]
public class ApplyBenchmarks
{
    private static readonly SomethingHappened Event = new();

    private readonly ReflectionSubject reflectionSubject = new("key");
    private readonly GeneratedSubject generatedSubject = new("key");

    [Benchmark(Baseline = true)]
    public int ApplyViaReflection()
    {
        this.reflectionSubject.DoSomething(Event);
        return this.reflectionSubject.Count;
    }

    [Benchmark]
    public int ApplyViaGeneratedDispatch()
    {
        this.generatedSubject.DoSomething(Event);
        return this.generatedSubject.Count;
    }

    public sealed class SomethingHappened
    {
    }

    public class ReflectionSubject : AggregateRoot
    {
        public ReflectionSubject(string key) => this.Key = key;

        [NaturalKey]
        public string Key { get; }

        public int Count { get; private set; }

        public void DoSomething(SomethingHappened @event) => this.Apply(@event);

        private void Handle(SomethingHappened @event) => this.Count++;
    }

    public partial class GeneratedSubject : AggregateRoot
    {
        public GeneratedSubject(string key) => this.Key = key;

        [NaturalKey]
        public string Key { get; }

        public int Count { get; private set; }

        public void DoSomething(SomethingHappened @event) => this.Apply(@event);

        private void Handle(SomethingHappened @event) => this.Count++;
    }
}
