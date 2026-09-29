using System.ComponentModel;

namespace dddlib.Sdk;

/// <summary>
/// Hides the members of <see cref="object"/> from IntelliSense on fluent interfaces.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IFluentExtensions
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    Type GetType();

    [EditorBrowsable(EditorBrowsableState.Never)]
    int GetHashCode();

    [EditorBrowsable(EditorBrowsableState.Never)]
    string? ToString();

    [EditorBrowsable(EditorBrowsableState.Never)]
    bool Equals(object? obj);
}
