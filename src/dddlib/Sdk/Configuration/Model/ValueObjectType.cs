using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using dddlib.Runtime;
using dddlib.Sdk.Generated;

namespace dddlib.Sdk.Configuration.Model;

/// <summary>
/// The runtime metadata for a value object type: its equality comparer, its serializer and its event mappings.
/// </summary>
public class ValueObjectType
{
    private Lazy<object> equalityComparer;

    public ValueObjectType(Type runtimeType, ITypeAnalyzerService typeAnalyzerService)
    {
        ArgumentNullException.ThrowIfNull(runtimeType);
        ArgumentNullException.ThrowIfNull(typeAnalyzerService);

        if (!typeAnalyzerService.IsValidValueObject(runtimeType))
        {
            throw new BusinessException(
                string.Format(CultureInfo.InvariantCulture, "The specified runtime type '{0}' is not a value object.", runtimeType));
        }

        this.RuntimeType = runtimeType;
        this.Serializer = (IValueObjectSerializer)CreateGeneric(typeof(DefaultValueObjectSerializer<>), runtimeType);
        this.equalityComparer = new Lazy<object>(
            () => GeneratedMetadata.TryGet<IGeneratedValueObjectMetadata>(runtimeType)?.EqualityComparer
                ?? CreateGeneric(typeof(DefaultValueObjectEqualityComparer<>), runtimeType),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public Type RuntimeType { get; }

    /// <summary>
    /// Gets the equality comparer, an <see cref="IEqualityComparer{T}"/> for <see cref="RuntimeType"/>. The default
    /// comparer is created on first use so that a bootstrapper can replace it first.
    /// </summary>
    public object EqualityComparer => this.equalityComparer.Value;

    public IValueObjectSerializer Serializer { get; private set; }

    public MapperCollection Mappings { get; } = new();

    public void ConfigureEqualityComparer<T>(IEqualityComparer<T> equalityComparer)
    {
        ArgumentNullException.ThrowIfNull(equalityComparer);

        var equalityComparerType = typeof(IEqualityComparer<>).MakeGenericType(this.RuntimeType);
        if (!equalityComparerType.IsInstanceOfType(equalityComparer))
        {
            throw new BusinessException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Invalid equality comparer. The specified equality comparer of type '{0}' does not match the required type of '{1}'.",
                    equalityComparer.GetType(),
                    equalityComparerType));
        }

        if (this.equalityComparer.IsValueCreated)
        {
            throw new BusinessException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "An attempt has been made to configure the equality comparer for value object of type '{0}' after it has already been used.",
                    this.RuntimeType));
        }

        this.equalityComparer = new Lazy<object>(() => equalityComparer);
    }

    public void ConfigureSerializer(IValueObjectSerializer valueObjectSerializer)
    {
        ArgumentNullException.ThrowIfNull(valueObjectSerializer);

        this.Serializer = valueObjectSerializer;
    }

    private static object CreateGeneric(Type genericTypeDefinition, Type typeArgument)
    {
        try
        {
            return Activator.CreateInstance(genericTypeDefinition.MakeGenericType(typeArgument))!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Throw(ex.InnerException);
            throw;
        }
    }
}
