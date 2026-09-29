using System.Text.Json;

namespace dddlib.Tests.Support;

/// <summary>
/// Renders a memento as JSON so two mementos can be compared structurally in an assertion.
/// </summary>
public static class MementoJson
{
    public static string Of(object? memento) =>
        memento is null ? "null" : JsonSerializer.Serialize(memento, memento.GetType());
}
