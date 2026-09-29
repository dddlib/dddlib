using System.Globalization;
using dddlib.Runtime;

namespace dddlib.Sdk.Configuration.Model;

/// <summary>
/// The runtime metadata for an aggregate root type: its event dispatcher and, when it can be reconstituted, the
/// factory that creates an uninitialized instance.
/// </summary>
public class AggregateRootType : EntityType
{
    public AggregateRootType(Type runtimeType, ITypeAnalyzerService typeAnalyzerService, EntityType baseEntity)
        : base(runtimeType, typeAnalyzerService, baseEntity)
    {
        if (!typeAnalyzerService.IsValidAggregateRoot(runtimeType))
        {
            throw new BusinessException(
                string.Format(CultureInfo.InvariantCulture, "The specified runtime type '{0}' is not an aggregate root.", runtimeType));
        }

        this.UninitializedFactory = typeAnalyzerService.GetUninitializedFactory(runtimeType);
        this.EventDispatcher = EventDispatchers.ForAggregateRoot(runtimeType);
    }

    public Delegate? UninitializedFactory { get; private set; }

    public IEventDispatcher EventDispatcher { get; }

    /// <summary>
    /// Gets a value indicating whether applied events are recorded for persistence. This is the case when the
    /// aggregate root can be reconstituted.
    /// </summary>
    public bool PersistEvents => this.UninitializedFactory is not null;

    public void ConfigureUninitializedFactory(Delegate uninitializedFactory)
    {
        ArgumentNullException.ThrowIfNull(uninitializedFactory);

        this.UninitializedFactory = uninitializedFactory;
    }
}
