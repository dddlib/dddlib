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

    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source) =>
        TestCompilation.AnalyzeAsync(source, new EventApplicationAnalyzer());
}
