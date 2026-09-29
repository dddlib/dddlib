namespace dddlib.Generators.Tests;

public class DomainTypeGeneratorTests
{
    private const string AggregateRootSource = """
        using dddlib;

        namespace Sample;

        public partial class Subject : AggregateRoot
        {
            public Subject(string id) => this.Apply(new NewSubject { Id = id });

            internal Subject() { }

            [NaturalKey]
            public string? Id { get; private set; }

            private void Handle(NewSubject @event) => this.Id = @event.Id;

            private void Handle(SubjectRenamed @event) { }
        }

        public class NewSubject { public string? Id { get; set; } }

        public sealed class SubjectRenamed { }
        """;

    [Test]
    public async Task GeneratesAggregateRootMetadata()
    {
        var run = TestCompilation.RunGenerators(AggregateRootSource, new DomainTypeGenerator());

        await Assert.That(run.Errors).IsEmpty();
        await Assert.That(run.Sources).Count().IsEqualTo(1);
        await Assert.That(run.AllGeneratedSource).Contains("IGeneratedAggregateRootMetadata");
        await Assert.That(run.AllGeneratedSource).Contains("public string? NaturalKeyPropertyName => \"Id\";");
        await Assert.That(run.AllGeneratedSource).Contains("typeof(string)");
        await Assert.That(run.AllGeneratedSource).Contains("(global::System.Func<global::Sample.Subject>)(static () => new global::Sample.Subject())");
        await Assert.That(run.AllGeneratedSource).Contains("if (eventType == typeof(global::Sample.NewSubject))");
        await Assert.That(run.AllGeneratedSource).Contains("self.Handle((global::Sample.SubjectRenamed)@event);");
    }

    [Test]
    public async Task GeneratesEntityMetadata()
    {
        var run = TestCompilation.RunGenerators("""
            using dddlib;

            namespace Sample;

            public partial class Thing : Entity
            {
                [NaturalKey]
                public System.Guid Id { get; set; }
            }
            """, new DomainTypeGenerator());

        await Assert.That(run.Errors).IsEmpty();
        await Assert.That(run.AllGeneratedSource).Contains("IGeneratedEntityMetadata");
        await Assert.That(run.AllGeneratedSource).Contains("typeof(global::System.Guid)");
        await Assert.That(run.AllGeneratedSource).Contains("((global::Sample.Thing)entity).Id;");
        await Assert.That(run.AllGeneratedSource).DoesNotContain("UninitializedFactory");
    }

    [Test]
    public async Task GeneratesValueObjectComparer()
    {
        var run = TestCompilation.RunGenerators("""
            using dddlib;

            namespace Sample;

            public partial class Money : ValueObject<Money>
            {
                public decimal Amount { get; set; }

                public string? Currency { get; set; }
            }
            """, new DomainTypeGenerator());

        await Assert.That(run.Errors).IsEmpty();
        await Assert.That(run.AllGeneratedSource).Contains("IEqualityComparer<global::Sample.Money>");
        await Assert.That(run.AllGeneratedSource).Contains("ValuesEqual(x.Amount, y.Amount)");
        await Assert.That(run.AllGeneratedSource).Contains("ValuesEqual(x.Currency, y.Currency));");
        await Assert.That(run.AllGeneratedSource).Contains("CombineHash(hash, obj.Currency);");
    }

    [Test]
    public async Task GeneratesIntoNestedPrivateTypes()
    {
        var run = TestCompilation.RunGenerators("""
            using dddlib;

            namespace Sample;

            public abstract partial class Feature
            {
                public sealed partial class Scenario
                {
                    private sealed partial class Subject : AggregateRoot
                    {
                        [NaturalKey]
                        public string? Key { get; private set; }

                        private void Handle(Created @event) => this.Key = @event.Key;
                    }

                    private sealed class Created { public string? Key { get; set; } }
                }
            }
            """, new DomainTypeGenerator());

        await Assert.That(run.Errors).IsEmpty();
        await Assert.That(run.AllGeneratedSource).Contains("partial class Feature");
        await Assert.That(run.AllGeneratedSource).Contains("partial class Scenario");
        await Assert.That(run.AllGeneratedSource).Contains("typeof(global::Sample.Feature.Scenario.Created)");
    }

    [Test]
    public async Task SkipsTypesThatAreNotPartial()
    {
        var run = TestCompilation.RunGenerators(AggregateRootSource.Replace("public partial class Subject", "public class Subject", StringComparison.Ordinal), new DomainTypeGenerator());

        await Assert.That(run.Errors).IsEmpty();
        await Assert.That(run.Sources).IsEmpty();
    }

    [Test]
    public async Task SkipsTypesWhoseContainingTypeIsNotPartial()
    {
        var run = TestCompilation.RunGenerators("""
            using dddlib;

            public class Outer
            {
                public partial class Subject : AggregateRoot { }
            }
            """, new DomainTypeGenerator());

        await Assert.That(run.Sources).IsEmpty();
    }

    [Test]
    public async Task SkipsEntitiesWithMoreThanOneNaturalKey()
    {
        var run = TestCompilation.RunGenerators("""
            using dddlib;

            public partial class Thing : Entity
            {
                [NaturalKey] public string? A { get; set; }
                [NaturalKey] public string? B { get; set; }
            }
            """, new DomainTypeGenerator());

        await Assert.That(run.Sources).IsEmpty();
    }

    [Test]
    public async Task SkipsValueObjectsWithoutPublicProperties()
    {
        var run = TestCompilation.RunGenerators("""
            using dddlib;

            public partial class Opaque : ValueObject<Opaque>
            {
                private readonly string value = "x";
            }
            """, new DomainTypeGenerator());

        await Assert.That(run.Sources).IsEmpty();
    }

    [Test]
    public async Task IgnoresPublicAndValueTypeHandlers()
    {
        var run = TestCompilation.RunGenerators("""
            using dddlib;

            public partial class Subject : AggregateRoot
            {
                public void Handle(Visible @event) { }
                private void Handle(int @event) { }
                private void Handle(Hidden @event) { }
            }

            public class Visible { }
            public class Hidden { }
            """, new DomainTypeGenerator());

        await Assert.That(run.Errors).IsEmpty();
        await Assert.That(run.AllGeneratedSource).Contains("typeof(global::Hidden)");
        await Assert.That(run.AllGeneratedSource).DoesNotContain("typeof(global::Visible)");
        await Assert.That(run.AllGeneratedSource).DoesNotContain("typeof(int)");
    }
}
