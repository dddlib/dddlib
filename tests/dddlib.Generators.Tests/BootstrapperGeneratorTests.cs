namespace dddlib.Generators.Tests;

public class BootstrapperGeneratorTests
{
    private const string Registration = "dddlib.BootstrapperRegistration.g.cs";

    [Test]
    public async Task RegistersTheSingleBootstrapper()
    {
        var run = TestCompilation.RunGenerators("""
            using dddlib.Configuration;

            namespace Sample;

            internal sealed class Bootstrapper : IBootstrapper
            {
                public void Bootstrap(IConfiguration configure) { }
            }
            """, new BootstrapperGenerator());

        await Assert.That(run.Errors).IsEmpty();
        await Assert.That(run.Sources.Keys).Contains(Registration);
        await Assert.That(run.Sources[Registration]).Contains("BootstrapperRegistry.Register(typeof(global::Sample.Bootstrapper));");
        await Assert.That(run.Sources[Registration]).Contains("ModuleInitializer");
    }

    [Test]
    public async Task RegistersNoneWhenTheAssemblyHasNoBootstrapper()
    {
        var run = TestCompilation.RunGenerators("""
            using dddlib;

            public class Thing : Entity { }
            """, new BootstrapperGenerator());

        await Assert.That(run.Errors).IsEmpty();
        await Assert.That(run.Sources[Registration]).Contains("BootstrapperRegistry.RegisterNone(");
    }

    [Test]
    public async Task EmitsNothingWhenThereAreSeveralBootstrappers()
    {
        var run = TestCompilation.RunGenerators("""
            using dddlib.Configuration;

            public class First : IBootstrapper { public void Bootstrap(IConfiguration configure) { } }
            public class Second : IBootstrapper { public void Bootstrap(IConfiguration configure) { } }
            """, new BootstrapperGenerator());

        await Assert.That(run.Sources).IsEmpty();
    }

    [Test]
    public async Task EmitsNothingWhenTheBootstrapperCannotBeInstantiated()
    {
        var run = TestCompilation.RunGenerators("""
            using dddlib.Configuration;

            public class Bootstrapper : IBootstrapper
            {
                public Bootstrapper(string irrelevant) { }
                public void Bootstrap(IConfiguration configure) { }
            }
            """, new BootstrapperGenerator());

        await Assert.That(run.Sources).IsEmpty();
    }

    [Test]
    public async Task EmitsNothingWhenTheBootstrapperIsNotAccessible()
    {
        var run = TestCompilation.RunGenerators("""
            using dddlib.Configuration;

            public class Outer
            {
                private sealed class Bootstrapper : IBootstrapper { public void Bootstrap(IConfiguration configure) { } }
            }
            """, new BootstrapperGenerator());

        await Assert.That(run.Sources).IsEmpty();
    }
}
