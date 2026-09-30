using dddlib.Generators;

namespace dddlib.CodeFixes.Tests;

public class CodeFixTests
{
    [Test]
    public async Task AddsAReconstitutionConstructorAfterTheLastConstructor()
    {
        var result = await CodeFixVerifier.ApplyAsync(
            """
            using dddlib;

            public partial class Subject : AggregateRoot
            {
                public Subject(string id)
                {
                    this.Id = id;
                }

                [NaturalKey]
                public string? Id { get; }
            }
            """,
            new DomainTypeAnalyzer(),
            new AddReconstitutionConstructorCodeFix(),
            "DDDLIB014");

        await Assert.That(result).IsEqualTo(CodeFixVerifier.Normalize(
            """
            using dddlib;

            public partial class Subject : AggregateRoot
            {
                public Subject(string id)
                {
                    this.Id = id;
                }

                // used for reconstitution only
                protected internal Subject()
                {
                }

                [NaturalKey]
                public string? Id { get; }
            }
            """));
    }

    [Test]
    public async Task AddsAPrivateReconstitutionConstructorToASealedType()
    {
        var result = await CodeFixVerifier.ApplyAsync(
            """
            using dddlib;

            namespace Sample
            {
                public sealed partial class Subject : AggregateRoot
                {
                    public Subject(string id) => this.Id = id;

                    [NaturalKey]
                    public string? Id { get; }
                }
            }
            """,
            new DomainTypeAnalyzer(),
            new AddReconstitutionConstructorCodeFix(),
            "DDDLIB014");

        await Assert.That(result).IsEqualTo(CodeFixVerifier.Normalize(
            """
            using dddlib;

            namespace Sample
            {
                public sealed partial class Subject : AggregateRoot
                {
                    public Subject(string id) => this.Id = id;

                    // used for reconstitution only
                    private Subject()
                    {
                    }

                    [NaturalKey]
                    public string? Id { get; }
                }
            }
            """));
    }

    [Test]
    public async Task MakesATypeAndItsContainingTypesPartial()
    {
        var result = await CodeFixVerifier.ApplyAsync(
            """
            using dddlib;

            public static class Model
            {
                internal sealed class Scenario
                {
                    // the subject
                    public class Subject : Entity
                    {
                    }
                }
            }
            """,
            new DomainTypeAnalyzer(),
            new MakePartialCodeFix(),
            "DDDLIB007");

        await Assert.That(result).IsEqualTo(CodeFixVerifier.Normalize(
            """
            using dddlib;

            public static partial class Model
            {
                internal sealed partial class Scenario
                {
                    // the subject
                    public partial class Subject : Entity
                    {
                    }
                }
            }
            """));
    }

    [Test]
    public async Task MakesATypeWithoutModifiersPartial()
    {
        var result = await CodeFixVerifier.ApplyAsync(
            """
            using dddlib;

            class Subject : Entity
            {
            }
            """,
            new DomainTypeAnalyzer(),
            new MakePartialCodeFix(),
            "DDDLIB007");

        await Assert.That(result).IsEqualTo(CodeFixVerifier.Normalize(
            """
            using dddlib;

            partial class Subject : Entity
            {
            }
            """));
    }

    [Test]
    public async Task MakesAPublicHandlerPrivate()
    {
        var result = await CodeFixVerifier.ApplyAsync(
            """
            using dddlib;

            public partial class Subject : AggregateRoot
            {
                [NaturalKey]
                public string? Id { get; set; }

                public void Handle(Renamed @event)
                {
                }
            }

            public class Renamed { }
            """,
            new DomainTypeAnalyzer(),
            new MakeHandlerPrivateCodeFix(),
            "DDDLIB003");

        await Assert.That(result).IsEqualTo(CodeFixVerifier.Normalize(
            """
            using dddlib;

            public partial class Subject : AggregateRoot
            {
                [NaturalKey]
                public string? Id { get; set; }

                private void Handle(Renamed @event)
                {
                }
            }

            public class Renamed { }
            """));
    }

    [Test]
    public async Task AddsAHandlerAfterTheLastHandler()
    {
        var result = await CodeFixVerifier.ApplyAsync(
            """
            using dddlib;

            namespace Sample.Model
            {
                public partial class Subject : AggregateRoot
                {
                    public void Rename() => this.Apply(new Events.Renamed());

                    private void Handle(Events.Created @event)
                    {
                    }

                    private void Unrelated()
                    {
                    }
                }
            }

            namespace Sample.Model.Events
            {
                public class Created { }

                public class Renamed { }
            }
            """,
            new EventApplicationAnalyzer(),
            new AddEventHandlerCodeFix(),
            "DDDLIB008");

        await Assert.That(result).IsEqualTo(CodeFixVerifier.Normalize(
            """
            using dddlib;

            namespace Sample.Model
            {
                public partial class Subject : AggregateRoot
                {
                    public void Rename() => this.Apply(new Events.Renamed());

                    private void Handle(Events.Created @event)
                    {
                    }

                    private void Handle(Events.Renamed @event)
                    {
                    }

                    private void Unrelated()
                    {
                    }
                }
            }

            namespace Sample.Model.Events
            {
                public class Created { }

                public class Renamed { }
            }
            """));
    }

    [Test]
    public async Task AddsAHandlerAtTheEndOfATypeWithoutHandlers()
    {
        var result = await CodeFixVerifier.ApplyAsync(
            """
            using dddlib;

            public partial class Subject : AggregateRoot
            {
                public void Rename() => this.Apply(new Renamed());
            }

            public class Renamed { }
            """,
            new EventApplicationAnalyzer(),
            new AddEventHandlerCodeFix(),
            "DDDLIB008");

        await Assert.That(result).IsEqualTo(CodeFixVerifier.Normalize(
            """
            using dddlib;

            public partial class Subject : AggregateRoot
            {
                public void Rename() => this.Apply(new Renamed());

                private void Handle(Renamed @event)
                {
                }
            }

            public class Renamed { }
            """));
    }

    [Test]
    public async Task AddsTheMissingSetStateOverride()
    {
        var result = await CodeFixVerifier.ApplyAsync(
            """
            using System;
            using dddlib;

            public partial class Subject : AggregateRoot
            {
                [NaturalKey]
                public string? Id { get; set; }

                protected override object? GetState() => this.Id;

                private void Unrelated()
                {
                }
            }
            """,
            new DomainTypeAnalyzer(),
            new AddMementoOverrideCodeFix(),
            "DDDLIB013");

        await Assert.That(result).IsEqualTo(CodeFixVerifier.Normalize(
            """
            using System;
            using dddlib;

            public partial class Subject : AggregateRoot
            {
                [NaturalKey]
                public string? Id { get; set; }

                protected override object? GetState() => this.Id;

                // TODO: restore the state from the memento; see https://github.com/dddlib/dddlib/blob/main/docs/aggregate-root-mementos.md
                protected override void SetState(object memento)
                {
                    throw new NotImplementedException();
                }

                private void Unrelated()
                {
                }
            }
            """));
    }

    [Test]
    public async Task AddsTheMissingGetStateOverride()
    {
        var result = await CodeFixVerifier.ApplyAsync(
            """
            using dddlib;

            public partial class Subject : AggregateRoot
            {
                [NaturalKey]
                public string? Id { get; set; }

                protected override void SetState(object memento)
                {
                    this.Id = (string)memento;
                }
            }
            """,
            new DomainTypeAnalyzer(),
            new AddMementoOverrideCodeFix(),
            "DDDLIB013");

        await Assert.That(result).IsEqualTo(CodeFixVerifier.Normalize(
            """
            using dddlib;

            public partial class Subject : AggregateRoot
            {
                [NaturalKey]
                public string? Id { get; set; }

                protected override void SetState(object memento)
                {
                    this.Id = (string)memento;
                }

                // TODO: return a memento of the state; see https://github.com/dddlib/dddlib/blob/main/docs/aggregate-root-mementos.md
                protected override object? GetState()
                {
                    return null;
                }
            }
            """));
    }

    [Test]
    public async Task OffersToRemoveEachNaturalKeyAttribute()
    {
        const string Source = """
            using dddlib;

            public partial class Thing : Entity
            {
                [NaturalKey]
                public string? First { get; set; }

                [NaturalKey, System.Obsolete]
                public string? Second { get; set; }
            }
            """;

        var titles = await CodeFixVerifier.GetTitlesAsync(Source, new DomainTypeAnalyzer(), new RemoveNaturalKeyCodeFix(), "DDDLIB001");
        var withoutFirst = await CodeFixVerifier.ApplyAsync(Source, new DomainTypeAnalyzer(), new RemoveNaturalKeyCodeFix(), "DDDLIB001", action: 0);
        var withoutSecond = await CodeFixVerifier.ApplyAsync(Source, new DomainTypeAnalyzer(), new RemoveNaturalKeyCodeFix(), "DDDLIB001", action: 1);

        await Assert.That(titles).IsEquivalentTo(["Remove [NaturalKey] from 'First'", "Remove [NaturalKey] from 'Second'"]);

        await Assert.That(withoutFirst).IsEqualTo(CodeFixVerifier.Normalize(
            """
            using dddlib;

            public partial class Thing : Entity
            {
                public string? First { get; set; }

                [NaturalKey, System.Obsolete]
                public string? Second { get; set; }
            }
            """));

        await Assert.That(withoutSecond).IsEqualTo(CodeFixVerifier.Normalize(
            """
            using dddlib;

            public partial class Thing : Entity
            {
                [NaturalKey]
                public string? First { get; set; }

                [System.Obsolete]
                public string? Second { get; set; }
            }
            """));
    }
}
