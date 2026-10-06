using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using dddlib.Runtime;
using dddlib.Sdk.Configuration.Model;

namespace dddlib.Persistence.Sdk;

public static class AggregateRootTypeExtensions
{
    public static void ValidateForPersistence(this AggregateRootType aggregateRootType)
    {
        ArgumentNullException.ThrowIfNull(aggregateRootType);

        if (aggregateRootType.NaturalKey is null)
        {
            throw new RuntimeException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    @"The aggregate root of type '{0}' does not have a natural key defined.
To fix this issue, either:
- use a bootstrapper to define a natural key, or
- decorate the natural key property on the aggregate root with the [dddlib.NaturalKey] attribute.",
                    aggregateRootType.RuntimeType))
            {
                HelpLink = "https://github.com/dddlib/dddlib/blob/main/docs/aggregate-root-equality.md",
            };
        }

        if (aggregateRootType.UninitializedFactory is null)
        {
            throw new RuntimeException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    @"The aggregate root of type '{0}' does not have a factory method registered with the runtime.
To fix this issue, either:
- use a bootstrapper to register a factory method with the runtime, or
- add a protected default constructor to the aggregate root.",
                    aggregateRootType.RuntimeType))
            {
                HelpLink = "https://github.com/dddlib/dddlib/blob/main/docs/aggregate-root-reconstitution.md",
            };
        }
    }

    public static void ValidateNaturalKey(this AggregateRootType aggregateRootType, object naturalKey)
    {
        ArgumentNullException.ThrowIfNull(aggregateRootType);
        ArgumentNullException.ThrowIfNull(naturalKey);

        if (aggregateRootType.NaturalKey!.PropertyType != naturalKey.GetType())
        {
            throw new ArgumentException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Invalid natural key value type for aggregate root of type '{0}'. Expected value type of '{1}' but value is type of '{2}'.",
                    aggregateRootType.RuntimeType,
                    aggregateRootType.NaturalKey.PropertyType,
                    naturalKey.GetType()),
                nameof(naturalKey));
        }
    }

    [DoesNotReturn]
    public static void ThrowNotFound(this AggregateRootType aggregateRootType, object naturalKey)
    {
        ArgumentNullException.ThrowIfNull(aggregateRootType);

        throw new AggregateRootNotFoundException(
            string.Format(
                CultureInfo.InvariantCulture,
                "Cannot find the aggregate root of type '{0}' with natural key '{1}'.",
                aggregateRootType.RuntimeType,
                naturalKey));
    }

    [SuppressMessage("Usage", "CA2208:Instantiate argument exceptions correctly", Justification = "The parameter path names the null natural key property (Bug0064).")]
    public static object GetNaturalKey(this AggregateRootType aggregateRootType, AggregateRoot aggregateRoot)
    {
        ArgumentNullException.ThrowIfNull(aggregateRootType);
        ArgumentNullException.ThrowIfNull(aggregateRoot);

        return aggregateRootType.NaturalKey!.GetValue(aggregateRoot)
            ?? throw new ArgumentException("Value cannot be null.", string.Concat(nameof(aggregateRoot), ".", aggregateRootType.NaturalKey.PropertyName));
    }
}
