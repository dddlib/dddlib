using dddlib.Runtime;
using dddlib.Sdk.Configuration;
using dddlib.Sdk.Configuration.Model;

namespace dddlib.Tests.Unit;

public class ApplicationTests
{
    [Test]
    public async Task CanCreateApplication()
    {
        var defaultApplication = Application.Current;

        using (new Application())
        {
            var currentApplication = Application.Current;

            await Assert.That(currentApplication).IsNotSameReferenceAs(defaultApplication);
        }
    }

    [Test]
    public async Task CanDisposeApplication()
    {
        var defaultApplication = Application.Current;

        using (new Application())
        {
        }

        var currentApplication = Application.Current;

        await Assert.That(currentApplication).IsSameReferenceAs(defaultApplication);
    }

    [Test]
    public async Task CanDisposeApplicationMultipleTimes()
    {
        var application = new Application();
        application.Dispose();

        var action = application.Dispose;

        await Assert.That(action).ThrowsNothing();
    }

    [Test]
    public async Task CanNestApplication()
    {
        var defaultApplication = Application.Current;

        using (new Application())
        {
            var firstApplication = Application.Current;

            using (new Application())
            {
                var secondApplication = Application.Current;

                await Assert.That(firstApplication).IsNotSameReferenceAs(defaultApplication);
                await Assert.That(secondApplication).IsNotSameReferenceAs(defaultApplication);
                await Assert.That(firstApplication).IsNotSameReferenceAs(secondApplication);
            }

            await Assert.That(Application.Current).IsSameReferenceAs(firstApplication);
        }
    }

    [Test]
    public async Task CannotDisposeDefaultApplication()
    {
        var defaultApplication = Application.Current;

        ((IDisposable)defaultApplication).Dispose();

        await Assert.That(Application.Current).IsSameReferenceAs(defaultApplication);
    }

    [Test]
    public async Task CannotUseDisposedApplication()
    {
        var application = new Application();
        application.Dispose();

        var action = () => application.GetAggregateRootType(typeof(Aggregate));

        await Assert.That(action).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task ApplicationCanCreateRuntimeTypeForValidType()
    {
        var typeAnalyzerService = new RecordingTypeAnalyzerService();

        using (new Application(typeAnalyzerService, new DefaultBootstrapperProvider()))
        {
            var actualType = Application.Current.GetAggregateRootType(typeof(Aggregate));
            var sameType = Application.Current.GetAggregateRootType(typeof(Aggregate));

            await Assert.That(actualType.RuntimeType).IsEqualTo(typeof(Aggregate));
            await Assert.That(sameType).IsSameReferenceAs(actualType);
            await Assert.That(typeAnalyzerService.AnalyzedTypes).Contains(typeof(Aggregate));
            await Assert.That(typeAnalyzerService.AnalyzedTypes.Count(type => type == typeof(Aggregate))).IsEqualTo(1);
        }
    }

    [Test]
    public async Task ApplicationThrowsRuntimeExceptionOnFactoryException()
    {
        var innerException = new InvalidOperationException();
        var typeAnalyzerService = new ThrowingTypeAnalyzerService(innerException);

        using (new Application(typeAnalyzerService, new DefaultBootstrapperProvider()))
        {
            var action = () => Application.Current.GetAggregateRootType(typeof(Aggregate));

            var exception = await Assert.That(action).Throws<RuntimeException>();
            await Assert.That(exception!.InnerException).IsSameReferenceAs(innerException);
        }
    }

    [Test]
    public async Task ApplicationThrowsRuntimeExceptionOnFactoryRuntimeException()
    {
        var runtimeException = new RuntimeException();
        var typeAnalyzerService = new ThrowingTypeAnalyzerService(runtimeException);

        using (new Application(typeAnalyzerService, new DefaultBootstrapperProvider()))
        {
            var action = () => Application.Current.GetAggregateRootType(typeof(Aggregate));

            var exception = await Assert.That(action).Throws<RuntimeException>();
            await Assert.That(exception).IsSameReferenceAs(runtimeException);
        }
    }

    private sealed class Aggregate : AggregateRoot
    {
    }

    private sealed class RecordingTypeAnalyzerService : ITypeAnalyzerService
    {
        private readonly DefaultTypeAnalyzerService inner = new();

        public List<Type> AnalyzedTypes { get; } = [];

        public bool IsValidAggregateRoot(Type runtimeType) => this.inner.IsValidAggregateRoot(runtimeType);

        public bool IsValidEntity(Type runtimeType) => this.inner.IsValidEntity(runtimeType);

        public bool IsValidValueObject(Type runtimeType) => this.inner.IsValidValueObject(runtimeType);

        public bool IsValidProperty(Type runtimeType, string propertyName, Type propertyType) =>
            this.inner.IsValidProperty(runtimeType, propertyName, propertyType);

        public NaturalKey? GetNaturalKey(Type runtimeType)
        {
            this.AnalyzedTypes.Add(runtimeType);
            return this.inner.GetNaturalKey(runtimeType);
        }

        public Delegate? GetUninitializedFactory(Type runtimeType) => this.inner.GetUninitializedFactory(runtimeType);
    }

    private sealed class ThrowingTypeAnalyzerService(Exception exception) : ITypeAnalyzerService
    {
        public bool IsValidAggregateRoot(Type runtimeType) => throw exception;

        public bool IsValidEntity(Type runtimeType) => throw exception;

        public bool IsValidValueObject(Type runtimeType) => throw exception;

        public bool IsValidProperty(Type runtimeType, string propertyName, Type propertyType) => throw exception;

        public NaturalKey? GetNaturalKey(Type runtimeType) => throw exception;

        public Delegate? GetUninitializedFactory(Type runtimeType) => throw exception;
    }
}
