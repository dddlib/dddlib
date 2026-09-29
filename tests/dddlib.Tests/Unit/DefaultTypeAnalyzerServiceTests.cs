using dddlib.Sdk.Configuration;

namespace dddlib.Tests.Unit;

public class DefaultTypeAnalyzerServiceTests
{
    [Test]
    [Arguments(typeof(DefaultInternalConstructorExample))]
    [Arguments(typeof(DefaultPublicConstructorExample))]
    [Arguments(typeof(DefaultPrivateConstructorExample))]
    [Arguments(typeof(DefaultProtectedConstructorExample))]
    public async Task CanGetUninitializedFactories(Type type)
    {
        var typeAnalyzer = new DefaultTypeAnalyzerService();

        var uninitializedFactory = typeAnalyzer.GetUninitializedFactory(type);
        var result = uninitializedFactory?.DynamicInvoke(null);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.GetType()).IsEqualTo(type);
    }

    [Test]
    public async Task CannotGetUninitializedFactoryForAbstractType()
    {
        var typeAnalyzer = new DefaultTypeAnalyzerService();

        var uninitializedFactory = typeAnalyzer.GetUninitializedFactory(typeof(AbstractExample));

        await Assert.That(uninitializedFactory).IsNull();
    }

    [Test]
    public async Task CannotGetUninitializedFactoryWithoutDefaultConstructor()
    {
        var typeAnalyzer = new DefaultTypeAnalyzerService();

        var uninitializedFactory = typeAnalyzer.GetUninitializedFactory(typeof(NoDefaultConstructorExample));

        await Assert.That(uninitializedFactory).IsNull();
    }

    public class DefaultInternalConstructorExample
    {
        public DefaultInternalConstructorExample(string irrelevant)
        {
            _ = irrelevant;
        }

        protected internal DefaultInternalConstructorExample()
        {
        }
    }

    public class DefaultPublicConstructorExample
    {
        public DefaultPublicConstructorExample()
        {
        }

        public DefaultPublicConstructorExample(string irrelevant)
        {
            _ = irrelevant;
        }
    }

    public class DefaultPrivateConstructorExample
    {
        public DefaultPrivateConstructorExample(string irrelevant)
        {
            _ = irrelevant;
        }

        private DefaultPrivateConstructorExample()
        {
        }
    }

    public class DefaultProtectedConstructorExample
    {
        public DefaultProtectedConstructorExample(string irrelevant)
        {
            _ = irrelevant;
        }

        protected DefaultProtectedConstructorExample()
        {
        }
    }

    public abstract class AbstractExample
    {
    }

    public class NoDefaultConstructorExample(string irrelevant)
    {
        public string Irrelevant { get; } = irrelevant;
    }
}
