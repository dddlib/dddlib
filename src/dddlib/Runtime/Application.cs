using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
using dddlib.Sdk.Configuration;
using dddlib.Sdk.Configuration.Model;

namespace dddlib.Runtime;

/// <summary>
/// The ambient registry of runtime type metadata for aggregate roots, entities and value objects.
/// Creating an instance makes it <see cref="Current"/> for the current asynchronous flow until it is disposed.
/// </summary>
public sealed class Application : IDisposable
{
    private static readonly ITypeAnalyzerService DefaultTypeAnalyzerService = new DefaultTypeAnalyzerService();
    private static readonly IBootstrapperProvider DefaultBootstrapperProvider = new DefaultBootstrapperProvider();
    private static readonly AsyncLocal<Application?> CurrentScope = new();
    private static readonly Lazy<Application> DefaultApplication = new(
        static () => new Application(DefaultTypeAnalyzerService, DefaultBootstrapperProvider, isDefault: true),
        LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly ConcurrentDictionary<Type, Lazy<AggregateRootType>> aggregateRootTypes = new();
    private readonly ConcurrentDictionary<Type, Lazy<EntityType>> entityTypes = new();
    private readonly ConcurrentDictionary<Type, Lazy<ValueObjectType>> valueObjectTypes = new();
    private readonly Func<Type, AggregateRootType> aggregateRootTypeFactory;
    private readonly Func<Type, EntityType> entityTypeFactory;
    private readonly Func<Type, ValueObjectType> valueObjectTypeFactory;
    private readonly Application? parent;
    private readonly bool isDefault;
    private bool isDisposed;

    public Application()
        : this(DefaultTypeAnalyzerService, DefaultBootstrapperProvider, isDefault: false)
    {
    }

    internal Application(IBootstrapperProvider bootstrapperProvider)
        : this(DefaultTypeAnalyzerService, bootstrapperProvider, isDefault: false)
    {
    }

    internal Application(ITypeAnalyzerService typeAnalyzerService, IBootstrapperProvider bootstrapperProvider)
        : this(typeAnalyzerService, bootstrapperProvider, isDefault: false)
    {
    }

    internal Application(
        Func<Type, AggregateRootType> aggregateRootTypeFactory,
        Func<Type, EntityType> entityTypeFactory,
        Func<Type, ValueObjectType> valueObjectTypeFactory)
        : this(aggregateRootTypeFactory, entityTypeFactory, valueObjectTypeFactory, isDefault: false)
    {
    }

    private Application(ITypeAnalyzerService typeAnalyzerService, IBootstrapperProvider bootstrapperProvider, bool isDefault)
        : this(
            new AggregateRootTypeFactory(typeAnalyzerService, bootstrapperProvider).Create,
            new EntityTypeFactory(typeAnalyzerService, bootstrapperProvider).Create,
            new ValueObjectTypeFactory(typeAnalyzerService, bootstrapperProvider).Create,
            isDefault)
    {
    }

    private Application(
        Func<Type, AggregateRootType> aggregateRootTypeFactory,
        Func<Type, EntityType> entityTypeFactory,
        Func<Type, ValueObjectType> valueObjectTypeFactory,
        bool isDefault)
    {
        ArgumentNullException.ThrowIfNull(aggregateRootTypeFactory);
        ArgumentNullException.ThrowIfNull(entityTypeFactory);
        ArgumentNullException.ThrowIfNull(valueObjectTypeFactory);

        this.aggregateRootTypeFactory = aggregateRootTypeFactory;
        this.entityTypeFactory = entityTypeFactory;
        this.valueObjectTypeFactory = valueObjectTypeFactory;
        this.isDefault = isDefault;

        if (!isDefault)
        {
            this.parent = CurrentScope.Value;
            CurrentScope.Value = this;
        }
    }

    /// <summary>
    /// Gets the current application: the innermost undisposed application created in the current asynchronous
    /// flow, or the default application if there is none.
    /// </summary>
    public static Application Current
    {
        get
        {
            var application = CurrentScope.Value;
            while (application is { isDisposed: true })
            {
                application = application.parent;
            }

            return application ?? DefaultApplication.Value;
        }
    }

    public void Dispose()
    {
        if (this.isDefault || this.isDisposed)
        {
            return;
        }

        this.isDisposed = true;

        if (ReferenceEquals(CurrentScope.Value, this))
        {
            CurrentScope.Value = this.parent;
        }
    }

    internal AggregateRootType GetAggregateRootType(Type type) =>
        this.GetRuntimeType(type, this.aggregateRootTypes, this.aggregateRootTypeFactory);

    internal EntityType GetEntityType(Type type) =>
        this.GetRuntimeType(type, this.entityTypes, this.entityTypeFactory);

    internal ValueObjectType GetValueObjectType(Type type) =>
        this.GetRuntimeType(type, this.valueObjectTypes, this.valueObjectTypeFactory);

    private static T Create<T>(Type type, Func<Type, T> factory)
    {
        try
        {
            return factory(type);
        }
        catch (RuntimeException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new RuntimeException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "The type factory for type '{0}' threw an exception during invocation.\r\nSee inner exception for details.",
                    type),
                ex);
        }
    }

    private T GetRuntimeType<T>(Type type, ConcurrentDictionary<Type, Lazy<T>> runtimeTypes, Func<Type, T> factory)
    {
        ArgumentNullException.ThrowIfNull(type);
        ObjectDisposedException.ThrowIf(this.isDisposed, this);

        var runtimeType = runtimeTypes.GetOrAdd(
            type,
            static (type, factory) => new Lazy<T>(() => Create(type, factory), LazyThreadSafetyMode.ExecutionAndPublication),
            factory);

        return runtimeType.Value;
    }
}
