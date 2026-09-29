using System.ComponentModel;
using dddlib.Runtime;

namespace dddlib.Sdk.Generated;

/// <summary>
/// Metadata about an aggregate root type emitted by the dddlib source generator. The dispatcher part covers the
/// event handlers declared on this type only.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IGeneratedAggregateRootMetadata : IGeneratedEntityMetadata, IEventDispatcher
{
    /// <summary>
    /// Gets a <c>Func&lt;T&gt;</c> that creates an uninitialized instance, or null if the type has no parameterless
    /// constructor or is abstract.
    /// </summary>
    Delegate? UninitializedFactory { get; }
}
