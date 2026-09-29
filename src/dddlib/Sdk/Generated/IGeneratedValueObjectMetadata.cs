using System.ComponentModel;

namespace dddlib.Sdk.Generated;

/// <summary>
/// Metadata about a value object type emitted by the dddlib source generator.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IGeneratedValueObjectMetadata
{
    /// <summary>
    /// Gets an <c>IEqualityComparer&lt;T&gt;</c> over the public properties of the value object.
    /// </summary>
    object EqualityComparer { get; }
}
