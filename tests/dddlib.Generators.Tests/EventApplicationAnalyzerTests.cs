using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;

namespace dddlib.Generators.Tests;

public class EventApplicationAnalyzerTests
{
    [Test]
    public async Task ReportsAnAppliedEventWithoutAHandler()
    {
        var diagnostics = await AnalyzeAsync("""
            using dddlib;

            public partial class Subject : AggregateRoot
            {
                public void Rename() => this.Apply(new Renamed());
            }

            public class Renamed { }
            """);

        var unhandled = diagnostics.Where(static d => d.Id == "DDDLIB008").ToList();

        await Assert.That(unhandled).Count().IsEqualTo(1);
        await Assert.That(unhandled[0].Severity).IsEqualTo(DiagnosticSeverity.Warning);
        await Assert.That(unhandled[0].GetMessage(CultureInfo.InvariantCulture)).Contains("Renamed");
        await Assert.That(unhandled[0].GetMessage(CultureInfo.InvariantCulture)).Contains("Subject");
    }

    [Test]
    public async Task DoesNotReportAnAppliedEventWithAHandler()
    {
        var diagnostics = await AnalyzeAsync("""
            using dddlib;

            public partial class Subject : AggregateRoot
            {
                public void Rename() => this.Apply(new Renamed());

                private void Handle(Renamed @event) { }
            }

            public class Renamed { }
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task DoesNotReportAHandlerDeclaredOnABaseClass()
    {
        var diagnostics = await AnalyzeAsync("""
            using dddlib;

            public abstract partial class Base : AggregateRoot
            {
                private void Handle(Renamed @event) { }
            }

            public partial class Subject : Base
            {
                public void Rename() => this.Apply(new Renamed());
            }

            public class Renamed { }
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task DoesNotReportWhenABaseClassInMetadataMayDeclareTheHandler()
    {
        // private members of a referenced assembly are not visible to the compiler, so the handler cannot be ruled out
        var reference = TestCompilation.Emit(
            """
            using dddlib;

            public abstract class Base : AggregateRoot
            {
                private void Handle(Renamed @event) { }
            }

            public class Renamed { }
            """,
            "BaseAssembly");

        var compilation = TestCompilation.Create(
            """
            public partial class Subject : Base
            {
                public void Rename() => this.Apply(new Renamed());
            }
            """,
            references: reference);

        var diagnostics = await TestCompilation.AnalyzeAsync(compilation, new EventApplicationAnalyzer());

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task DoesNotReportAnArgumentTypedAsABaseOfAHandledType()
    {
        // the runtime type of the argument is not known, and may be the handled type
        var diagnostics = await AnalyzeAsync("""
            using dddlib;

            public partial class Subject : AggregateRoot
            {
                public void Change(Changed @event) => this.Apply(@event);

                private void Handle(Renamed @event) { }
            }

            public class Changed { }

            public class Renamed : Changed { }
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task ReportsANewEventWhoseOnlyHandlerTakesADerivedType()
    {
        var diagnostics = await AnalyzeAsync("""
            using dddlib;

            public partial class Subject : AggregateRoot
            {
                public void Change() => this.Apply(new Changed());

                private void Handle(Renamed @event) { }
            }

            public class Changed { }

            public class Renamed : Changed { }
            """);

        await Assert.That(diagnostics.Count(static d => d.Id == "DDDLIB008")).IsEqualTo(1);
    }

    [Test]
    public async Task ReportsAnEventWhoseOnlyHandlerTakesABaseType()
    {
        var diagnostics = await AnalyzeAsync("""
            using dddlib;

            public partial class Subject : AggregateRoot
            {
                public void Rename() => this.Apply(new Renamed());

                private void Handle(Changed @event) { }
            }

            public class Changed { }

            public class Renamed : Changed { }
            """);

        await Assert.That(diagnostics.Count(static d => d.Id == "DDDLIB008")).IsEqualTo(1);
    }

    [Test]
    public async Task DoesNotReportAnEventWhoseTypeIsNotKnown()
    {
        var diagnostics = await AnalyzeAsync("""
            using dddlib;

            public partial class Subject : AggregateRoot
            {
                public void Raise<T>(T @event)
                    where T : class, new()
                {
                    this.Apply(@event);
                }

                public void Raise(object @event) => this.Apply(@event);
            }
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task ReportsAHandlerThatAppliesAnEvent()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;
            using dddlib;

            public partial class Subject : AggregateRoot
            {
                public void Rename() => this.Apply(new Renamed());

                private void Handle(Renamed @event)
                {
                    this.Apply(new Audited());

                    Action later = () => this.Apply(new Audited());
                    later();
                }

                private void Handle(Audited @event) { }
            }

            public class Renamed { }

            public class Audited { }
            """);

        var applied = diagnostics.Where(static d => d.Id == "DDDLIB010").ToList();

        await Assert.That(diagnostics).Count().IsEqualTo(2);
        await Assert.That(applied).Count().IsEqualTo(2);
        await Assert.That(applied[0].Severity).IsEqualTo(DiagnosticSeverity.Warning);
        await Assert.That(applied[0].GetMessage(CultureInfo.InvariantCulture)).Contains("Subject");
    }

    [Test]
    public async Task DoesNotReportApplyOutsideAHandler()
    {
        var diagnostics = await AnalyzeAsync("""
            using dddlib;

            public partial class Subject : AggregateRoot
            {
                public void Rename() => this.Apply(new Renamed());

                // public, so not a handler the dispatcher calls
                public void Handle(Audited @event) => this.Apply(new Renamed());

                private void Handle(Renamed @event) { }
            }

            public class Renamed { }

            public class Audited { }
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task ReportsAHandlerThatThrows()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;
            using dddlib;

            public partial class Subject : AggregateRoot
            {
                public string Name { get; private set; } = string.Empty;

                private void Handle(Renamed @event)
                {
                    if (@event.Name == this.Name)
                    {
                        throw new BusinessException("The name has not changed.");
                    }

                    this.Name = @event.Name ?? throw new ArgumentException("The name is missing.");
                }
            }

            public class Renamed
            {
                public string? Name { get; set; }
            }
            """);

        var throwing = diagnostics.Where(static d => d.Id == "DDDLIB011").ToList();

        await Assert.That(diagnostics).Count().IsEqualTo(2);
        await Assert.That(throwing).Count().IsEqualTo(2);
        await Assert.That(throwing[0].Severity).IsEqualTo(DiagnosticSeverity.Warning);
        await Assert.That(throwing[0].GetMessage(CultureInfo.InvariantCulture)).Contains("Subject");
    }

    [Test]
    public async Task DoesNotReportAThrowOutsideAHandler()
    {
        var diagnostics = await AnalyzeAsync("""
            using dddlib;

            public partial class Subject : AggregateRoot
            {
                public string Name { get; private set; } = string.Empty;

                public void Rename(string name)
                {
                    if (name == this.Name)
                    {
                        throw new BusinessException("The name has not changed.");
                    }

                    this.Apply(new Renamed { Name = name });
                }

                private void Handle(Renamed @event) => this.Name = @event.Name!;
            }

            public class Renamed
            {
                public string? Name { get; set; }
            }
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task ReportsEventPropertiesThatAreSavedButNotLoaded()
    {
        var diagnostics = await AnalyzeAsync("""
            using dddlib;

            public partial class Subject : AggregateRoot
            {
                public void Rename(string name)
                {
                    this.Apply(new Renamed(name));
                    this.Apply(new Renamed(name));
                }

                private void Handle(Renamed @event) { }

                // handled only, as when it is applied by another class of the hierarchy
                private void Handle(Moved @event) { }
            }

            public class Renamed
            {
                public Renamed() { }
                public Renamed(string name) { this.Name = name; }
                public string? Name { get; }
                public string? Reason { get; set; }
                public int Length => this.Name?.Length ?? 0;
            }

            public class Moved
            {
                public string? Place { get; private set; }
            }
            """);

        var unloaded = diagnostics.Where(static d => d.Id == "DDDLIB012").Select(static d => d.GetMessage(CultureInfo.InvariantCulture)).ToList();

        await Assert.That(diagnostics).Count().IsEqualTo(2);
        await Assert.That(unloaded.Count(static message => message.Contains("'Name' of the event 'Renamed'", StringComparison.Ordinal))).IsEqualTo(1);
        await Assert.That(unloaded.Count(static message => message.Contains("'Place' of the event 'Moved'", StringComparison.Ordinal))).IsEqualTo(1);
        await Assert.That(diagnostics[0].Location.SourceTree!.GetText().ToString(diagnostics[0].Location.SourceSpan)).IsIn("Name", "Place");
    }

    [Test]
    public async Task ReportsMementoPropertiesThatAreSavedButNotLoaded()
    {
        var diagnostics = await AnalyzeAsync("""
            using dddlib;

            public partial class Subject : AggregateRoot
            {
                private string name = string.Empty;

                protected override object? GetState() => new Memento(this.name);

                protected override void SetState(object memento) => this.name = ((Memento)memento).Name;
            }

            public class Memento
            {
                public Memento() { }
                public Memento(string name) { this.Name = name; }
                public string Name { get; } = string.Empty;
            }
            """);

        await Assert.That(diagnostics).Count().IsEqualTo(1);
        await Assert.That(diagnostics[0].Id).IsEqualTo("DDDLIB012");
        await Assert.That(diagnostics[0].GetMessage(CultureInfo.InvariantCulture)).Contains("'Name' of the memento 'Memento'");
    }

    [Test]
    public async Task DoesNotReportEventsAndMementosThatAreLoadedInFull()
    {
        var diagnostics = await AnalyzeAsync("""
            using dddlib;

            public partial class Subject : AggregateRoot
            {
                private string name = string.Empty;

                public void Rename(string name) => this.Apply(new Renamed { Name = name });

                protected override object? GetState() => new Memento(this.name);

                protected override void SetState(object memento) => this.name = ((Memento)memento).Name;

                private void Handle(Renamed @event) => this.name = @event.Name!;
            }

            public class Renamed
            {
                public string? Name { get; set; }
            }

            public class Memento
            {
                public Memento(string name) { this.Name = name; }
                public string Name { get; }
            }

            // neither an event nor a memento
            public class Unrelated
            {
                public string Name { get; } = string.Empty;
            }
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task DoesNotReportAnEventDeclaredInAnotherAssembly()
    {
        var reference = TestCompilation.Emit(
            """
            public class Renamed
            {
                public string Name { get; } = string.Empty;
            }
            """,
            "EventAssembly");

        var compilation = TestCompilation.Create(
            """
            using dddlib;

            public partial class Subject : AggregateRoot
            {
                public void Rename() => this.Apply(new Renamed());

                private void Handle(Renamed @event) { }
            }
            """,
            references: reference);

        var diagnostics = await TestCompilation.AnalyzeAsync(compilation, new EventApplicationAnalyzer());

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task ReportsEventsThatCannotBeLoaded()
    {
        var diagnostics = await AnalyzeAsync("""
            using dddlib;

            public partial class Subject : AggregateRoot
            {
                public void Rename(string name)
                {
                    this.Apply(new Renamed(name));
                    this.Apply(new Renamed(name));
                    this.Apply(Moved.To(name));
                }

                private void Handle(Renamed @event) { }

                private void Handle(Moved @event) { }

                // handled only, as when it is applied by another class of the hierarchy
                private void Handle(Closed @event) { }
            }

            public class Renamed
            {
                public Renamed(string name) { this.Name = name; }
                public Renamed(string name, string reason) { this.Name = name; this.Reason = reason; }
                public string Name { get; }
                public string? Reason { get; }
            }

            public class Moved
            {
                private Moved(string place) { this.Place = place; }
                public string Place { get; }
                public static Moved To(string place) => new Moved(place);
            }

            public class Closed
            {
                public Closed(string why) { this.Reason = why; }
                public string Reason { get; }
            }
            """);

        var messages = diagnostics.Select(static d => d.GetMessage(CultureInfo.InvariantCulture)).ToList();

        await Assert.That(diagnostics).Count().IsEqualTo(3);
        await Assert.That(diagnostics.Select(static d => d.Id).Distinct()).IsEquivalentTo(["DDDLIB023"]);
        await Assert.That(messages.Count(static message => message.Contains("The event 'Renamed'", StringComparison.Ordinal) && message.Contains("[JsonConstructor]", StringComparison.Ordinal))).IsEqualTo(1);
        await Assert.That(messages.Count(static message => message.Contains("The event 'Moved'", StringComparison.Ordinal) && message.Contains("[JsonConstructor]", StringComparison.Ordinal))).IsEqualTo(1);
        await Assert.That(messages.Count(static message => message.Contains("The event 'Closed'", StringComparison.Ordinal) && message.Contains("parameter 'why'", StringComparison.Ordinal))).IsEqualTo(1);
        await Assert.That(diagnostics[0].Location.SourceTree!.GetText().ToString(diagnostics[0].Location.SourceSpan)).IsIn("Renamed", "Moved", "Closed");
    }

    [Test]
    public async Task ReportsAMementoThatCannotBeLoaded()
    {
        var diagnostics = await AnalyzeAsync("""
            using dddlib;

            public partial class Subject : AggregateRoot
            {
                private string name = string.Empty;

                protected override object? GetState() => new Memento(this.name);

                protected override void SetState(object memento) => this.name = ((Memento)memento).Name;
            }

            public class Memento
            {
                public Memento(string name) { this.Name = name; }
                public Memento(string name, int revision) { this.Name = name; this.Revision = revision; }
                public string Name { get; }
                public int Revision { get; }
            }
            """);

        await Assert.That(diagnostics).Count().IsEqualTo(1);
        await Assert.That(diagnostics[0].Id).IsEqualTo("DDDLIB023");
        await Assert.That(diagnostics[0].GetMessage(CultureInfo.InvariantCulture)).Contains("The memento 'Memento'");
    }

    [Test]
    public async Task DoesNotReportEventsAndMementosThatAConstructorLoads()
    {
        var diagnostics = await AnalyzeAsync("""
            using System.Text.Json.Serialization;
            using dddlib;

            public partial class Subject : AggregateRoot
            {
                private string name = string.Empty;

                public void Rename(string name) => this.Apply(new Renamed(name));

                public void Move(string place) => this.Apply(new Moved(place));

                public void Change(Changed changed) => this.Apply(changed);

                protected override object? GetState() => new Memento(this.name);

                protected override void SetState(object memento) => this.name = ((Memento)memento).Name;

                private void Handle(Renamed @event) => this.name = @event.Name;

                private void Handle(Moved @event) { }
            }

            public record Renamed(string Name);

            public class Moved
            {
                [JsonConstructor]
                public Moved(string place) { this.Place = place; }
                public Moved(string place, int distance) : this(place) { }
                public string Place { get; }
            }

            // never the runtime type of an event, so how it is constructed says nothing
            public abstract class Changed
            {
                protected Changed(string why) { this.Reason = why; }
                public string Reason { get; }
            }

            public sealed record Memento(string Name);
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source) =>
        TestCompilation.AnalyzeAsync(source, new EventApplicationAnalyzer());
}
