using Microsoft.CodeAnalysis;

namespace dddlib.Generators.Tests;

public class BootstrapperModelTests
{
    private const string Preamble = """
        using System;
        using System.Collections.Generic;
        using System.Linq.Expressions;
        using dddlib;
        using dddlib.Configuration;

        public class Order : AggregateRoot
        {
            public string? Id { get; set; }
            public string? Reference { get; set; }
        }

        public class Line : Entity
        {
            public string? Sku { get; set; }
        }

        public class Money : ValueObject<Money>
        {
            public decimal Amount { get; set; }
        }

        public class LineAdded { }

        public class MoneyChanged { }

        """;

    [Test]
    public async Task RecordsToReconstituteUsing()
    {
        var (model, compilation) = Bootstrap("configure.AggregateRoot<Order>().ToReconstituteUsing(() => new Order());");

        await Assert.That(model.IsKnown).IsTrue();
        await Assert.That(model.GetConfiguration(Type(compilation, "Order")).HasReconstitutionFactory).IsTrue();
        await Assert.That(model.GetConfiguration(Type(compilation, "Line")).HasReconstitutionFactory).IsFalse();
    }

    [Test]
    public async Task RecordsToUseNaturalKeyWithTheSelectedProperty()
    {
        var (model, compilation) = Bootstrap("""
            configure.AggregateRoot<Order>().ToUseNaturalKey(order => order.Reference);
            configure.Entity<Line>().ToUseNaturalKey(line => line.Sku);
            """);

        var order = model.GetConfiguration(Type(compilation, "Order")).NaturalKeys;
        var line = model.GetConfiguration(Type(compilation, "Line")).NaturalKeys;

        await Assert.That(model.IsKnown).IsTrue();
        await Assert.That(order.Length).IsEqualTo(1);
        await Assert.That(order[0].IsLambda).IsTrue();
        await Assert.That(order[0].Property?.Name).IsEqualTo("Reference");
        await Assert.That(order[0].Location.IsInSource).IsTrue();
        await Assert.That(line.Length).IsEqualTo(1);
        await Assert.That(line[0].Property?.Name).IsEqualTo("Sku");
    }

    [Test]
    public async Task RecordsAnAggregateRootConfiguredAsAnEntity()
    {
        var (model, compilation) = Bootstrap("configure.Entity<Order>().ToUseNaturalKey(order => order.Id);");

        await Assert.That(model.GetConfiguration(Type(compilation, "Order")).NaturalKeys[0].Property?.Name).IsEqualTo("Id");
    }

    [Test]
    public async Task MarksANaturalKeySelectorThatIsNotAPropertyOfTheParameter()
    {
        var (model, compilation) = Bootstrap("""
            configure.AggregateRoot<Order>().ToUseNaturalKey(order => order.Id + order.Reference);
            configure.Entity<Line>().ToUseNaturalKey(line => line.Sku!.Length);
            """);

        var order = model.GetConfiguration(Type(compilation, "Order")).NaturalKeys[0];
        var line = model.GetConfiguration(Type(compilation, "Line")).NaturalKeys[0];

        await Assert.That(model.IsKnown).IsTrue();
        await Assert.That(order.IsLambda).IsTrue();
        await Assert.That(order.Property).IsNull();
        await Assert.That(line.IsLambda).IsTrue();
        await Assert.That(line.Property).IsNull();
    }

    [Test]
    public async Task MarksANaturalKeySelectorThatIsNotALambda()
    {
        var (model, compilation) = Bootstrap("""
            Expression<Func<Order, string?>> selector = order => order.Id;
            configure.AggregateRoot<Order>().ToUseNaturalKey(selector);
            """);

        var selection = model.GetConfiguration(Type(compilation, "Order")).NaturalKeys[0];

        await Assert.That(model.IsKnown).IsTrue();
        await Assert.That(selection.IsLambda).IsFalse();
        await Assert.That(selection.Property).IsNull();
    }

    [Test]
    public async Task RecordsToUseEqualityComparer()
    {
        var (model, compilation) = Bootstrap("configure.ValueObject<Money>().ToUseEqualityComparer(EqualityComparer<Money>.Default);");

        var money = model.GetConfiguration(Type(compilation, "Money"));

        await Assert.That(money.HasEqualityComparer).IsTrue();
        await Assert.That(money.HasValueObjectSerializer).IsFalse();
    }

    [Test]
    public async Task RecordsToUseValueObjectSerializer()
    {
        var (model, compilation) = Bootstrap("configure.ValueObject<Money>().ToUseValueObjectSerializer(money => string.Empty, text => new Money());");

        var money = model.GetConfiguration(Type(compilation, "Money"));

        await Assert.That(money.HasValueObjectSerializer).IsTrue();
        await Assert.That(money.HasEqualityComparer).IsFalse();
    }

    [Test]
    public async Task RecordsToMapToEventWithoutTheReverseMapping()
    {
        var (model, compilation) = Bootstrap("configure.Entity<Line>().ToMapToEvent<LineAdded>((line, @event) => { });");

        var line = model.GetConfiguration(Type(compilation, "Line"));

        await Assert.That(line.MapsToEvent(Type(compilation, "LineAdded"))).IsTrue();
        await Assert.That(line.MapsFromEvent(Type(compilation, "LineAdded"))).IsFalse();
        await Assert.That(line.MapsToEvent(Type(compilation, "MoneyChanged"))).IsFalse();
    }

    [Test]
    public async Task RecordsToMapToEventWithTheReverseMapping()
    {
        var (model, compilation) = Bootstrap("""
            configure.Entity<Line>().ToMapToEvent<LineAdded>((line, @event) => { }, @event => new Line());
            configure.ValueObject<Money>().ToMapToEvent<MoneyChanged>((money, @event) => { }, @event => new Money());
            """);

        var line = model.GetConfiguration(Type(compilation, "Line"));
        var money = model.GetConfiguration(Type(compilation, "Money"));

        await Assert.That(line.MapsToEvent(Type(compilation, "LineAdded"))).IsTrue();
        await Assert.That(line.MapsFromEvent(Type(compilation, "LineAdded"))).IsTrue();
        await Assert.That(money.MapsToEvent(Type(compilation, "MoneyChanged"))).IsTrue();
        await Assert.That(money.MapsFromEvent(Type(compilation, "MoneyChanged"))).IsTrue();
    }

    [Test]
    public async Task MergesEveryChainForTheSameType()
    {
        var (model, compilation) = Bootstrap("""
            configure.AggregateRoot<Order>()
                .ToReconstituteUsing(() => new Order())
                .ToUseNaturalKey(order => order.Id);

            configure.Entity<Line>().ToMapToEvent<LineAdded>((line, @event) => { });
            configure.Entity<Line>().ToMapToEvent<LineAdded>((line, @event) => { }, @event => new Line());
            configure.Entity<Line>().ToUseNaturalKey(line => line.Sku);
            """);

        var order = model.GetConfiguration(Type(compilation, "Order"));
        var line = model.GetConfiguration(Type(compilation, "Line"));

        await Assert.That(order.HasReconstitutionFactory).IsTrue();
        await Assert.That(order.NaturalKeys.Length).IsEqualTo(1);
        await Assert.That(line.MapsFromEvent(Type(compilation, "LineAdded"))).IsTrue();
        await Assert.That(line.NaturalKeys.Length).IsEqualTo(1);
    }

    [Test]
    public async Task ReadsConfigurationInsideControlFlow()
    {
        var (model, compilation) = Bootstrap("""
            if (DateTime.UtcNow.Year > 2000)
            {
                configure.AggregateRoot<Order>().ToReconstituteUsing(() => new Order());
            }
            """);

        await Assert.That(model.IsKnown).IsTrue();
        await Assert.That(model.GetConfiguration(Type(compilation, "Order")).HasReconstitutionFactory).IsTrue();
    }

    [Test]
    public async Task ReadsAnExpressionBodiedBootstrapMethod()
    {
        var (model, compilation) = Compile("""
            internal sealed class Bootstrapper : IBootstrapper
            {
                public void Bootstrap(IConfiguration configure) => configure.AggregateRoot<Order>().ToReconstituteUsing(() => new Order());
            }
            """);

        await Assert.That(model.IsKnown).IsTrue();
        await Assert.That(model.GetConfiguration(Type(compilation, "Order")).HasReconstitutionFactory).IsTrue();
    }

    [Test]
    public async Task ReadsAnExplicitlyImplementedBootstrapMethod()
    {
        var (model, compilation) = Compile("""
            internal sealed class Bootstrapper : IBootstrapper
            {
                void IBootstrapper.Bootstrap(IConfiguration configure)
                {
                    configure.AggregateRoot<Order>().ToReconstituteUsing(() => new Order());
                }
            }
            """);

        await Assert.That(model.IsKnown).IsTrue();
        await Assert.That(model.GetConfiguration(Type(compilation, "Order")).HasReconstitutionFactory).IsTrue();
    }

    [Test]
    public async Task AnEmptyBootstrapperIsKnownAndConfiguresNothing()
    {
        var (model, compilation) = Bootstrap(string.Empty);

        var order = model.GetConfiguration(Type(compilation, "Order"));

        await Assert.That(model.IsKnown).IsTrue();
        await Assert.That(order.HasReconstitutionFactory).IsFalse();
        await Assert.That(order.NaturalKeys.Length).IsEqualTo(0);
    }

    [Test]
    public async Task IsKnownAndConfiguresNothingWithoutABootstrapper()
    {
        var (model, compilation) = Compile(string.Empty);

        await Assert.That(model.IsKnown).IsTrue();
        await Assert.That(model.GetConfiguration(Type(compilation, "Order")).HasReconstitutionFactory).IsFalse();
    }

    [Test]
    public async Task IsUnknownWithoutABootstrapperWhenAMethodTakesTheConfiguration()
    {
        var (model, _) = Compile("""
            internal sealed class OrderConfiguration
            {
                public void Bootstrap(IConfiguration configure) => configure.AggregateRoot<Order>().ToReconstituteUsing(() => new Order());
            }
            """);

        await Assert.That(model.IsKnown).IsFalse();
    }

    [Test]
    public async Task IsUnknownWithMoreThanOneBootstrapper()
    {
        var (model, _) = Compile("""
            public class First : IBootstrapper { public void Bootstrap(IConfiguration configure) { } }
            public class Second : IBootstrapper { public void Bootstrap(IConfiguration configure) { } }
            """);

        await Assert.That(model.IsKnown).IsFalse();
    }

    [Test]
    public async Task IsUnknownWhenTheConfigurationIsPassedToAHelper()
    {
        var (model, _) = Bootstrap("""
            Action<IConfiguration> helper = _ => { };
            helper(configure);
            """);

        await Assert.That(model.IsKnown).IsFalse();
    }

    [Test]
    public async Task IsUnknownWhenTheConfigurationIsAssigned()
    {
        var (model, _) = Bootstrap("""
            var copy = configure;
            copy.AggregateRoot<Order>().ToReconstituteUsing(() => new Order());
            """);

        await Assert.That(model.IsKnown).IsFalse();
    }

    [Test]
    public async Task IsUnknownWhenAWrapperLeavesItsChain()
    {
        var (model, _) = Bootstrap("""
            var order = configure.AggregateRoot<Order>();
            order.ToReconstituteUsing(() => new Order());
            """);

        await Assert.That(model.IsKnown).IsFalse();
    }

    [Test]
    public async Task IsUnknownWhenAnotherMethodTakesTheConfiguration()
    {
        // configuration handed out by a custom IBootstrapperProvider, which the analyzer cannot follow
        var (model, _) = Compile("""
            internal sealed class Bootstrapper : IBootstrapper
            {
                public void Bootstrap(IConfiguration configure) { }
            }

            internal sealed class OrderConfiguration
            {
                public void Bootstrap(IConfiguration configure) => configure.AggregateRoot<Order>().ToReconstituteUsing(() => new Order());
            }
            """);

        await Assert.That(model.IsKnown).IsFalse();
    }

    [Test]
    public async Task UnknownConfiguresNothing()
    {
        var (model, compilation) = Bootstrap("""
            var copy = configure;
            copy.AggregateRoot<Order>().ToReconstituteUsing(() => new Order());
            """);

        var order = model.GetConfiguration(Type(compilation, "Order"));

        await Assert.That(model.IsKnown).IsFalse();
        await Assert.That(order.HasReconstitutionFactory).IsFalse();
        await Assert.That(order.NaturalKeys.Length).IsEqualTo(0);
    }

    [Test]
    public async Task SharesOneModelPerCompilation()
    {
        var (_, compilation) = Bootstrap(string.Empty);
        var known = KnownSymbols.Create(compilation)!;

        var first = BootstrapperModel.GetLazy(compilation, known);
        var second = BootstrapperModel.GetLazy(compilation, KnownSymbols.Create(compilation)!);

        await Assert.That(ReferenceEquals(first, second)).IsTrue();
        await Assert.That(first.Value.IsKnown).IsTrue();
    }

    private static (BootstrapperModel Model, Compilation Compilation) Bootstrap(string body) => Compile($$"""
        internal sealed class Bootstrapper : IBootstrapper
        {
            public void Bootstrap(IConfiguration configure)
            {
                {{body}}
            }
        }
        """);

    private static (BootstrapperModel Model, Compilation Compilation) Compile(string source)
    {
        var compilation = TestCompilation.Create(Preamble + source);

        // a source that does not compile must not pass for an unknown model
        var errors = compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToList();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        return (BootstrapperModel.Create(compilation, KnownSymbols.Create(compilation)!), compilation);
    }

    private static INamedTypeSymbol Type(Compilation compilation, string name) => compilation.GetTypeByMetadataName(name)!;
}
