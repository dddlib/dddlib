using System.Globalization;
using dddlib.Runtime;

namespace dddlib.Sdk.Configuration.Model;

/// <summary>
/// The runtime metadata for a value object type: its serializer and its event mappings.
/// </summary>
public class ValueObjectType
{
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
        this.Serializer = (IValueObjectSerializer)Activator.CreateInstance(
            typeof(DefaultValueObjectSerializer<>).MakeGenericType(runtimeType))!;
    }

    public Type RuntimeType { get; }

    public IValueObjectSerializer Serializer { get; private set; }

    public MapperCollection Mappings { get; } = new();

    public void ConfigureSerializer(IValueObjectSerializer valueObjectSerializer)
    {
        ArgumentNullException.ThrowIfNull(valueObjectSerializer);

        this.Serializer = valueObjectSerializer;
    }
}
