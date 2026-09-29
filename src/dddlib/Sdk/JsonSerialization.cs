using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace dddlib.Sdk;

/// <summary>
/// The System.Text.Json options dddlib uses for events, mementos and natural keys. Source-generated
/// <see cref="System.Text.Json.Serialization.JsonSerializerContext"/> instances can be registered before first use;
/// reflection-based serialization remains the fallback for types they do not cover.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class JsonSerialization
{
    private static readonly Lock Sync = new();
    private static readonly List<IJsonTypeInfoResolver> Resolvers = [];
    private static JsonSerializerOptions? options;

    /// <summary>
    /// Gets the options. DateTime and DateTimeOffset are written as ISO 8601 round-trip strings.
    /// </summary>
    public static JsonSerializerOptions Options
    {
        get
        {
            lock (Sync)
            {
                return options ??= Build();
            }
        }
    }

    /// <summary>
    /// Registers a type info resolver, typically a generated <see cref="System.Text.Json.Serialization.JsonSerializerContext"/>,
    /// ahead of the reflection fallback. Must be called before <see cref="Options"/> is first used.
    /// </summary>
    public static void AddTypeInfoResolver(IJsonTypeInfoResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);

        lock (Sync)
        {
            if (options is not null)
            {
                throw new InvalidOperationException("Type info resolvers must be registered before the JSON options are first used.");
            }

            Resolvers.Add(resolver);
        }
    }

    private static JsonSerializerOptions Build()
    {
        var built = new JsonSerializerOptions(JsonSerializerDefaults.General);
        if (Resolvers.Count > 0)
        {
            built.TypeInfoResolver = JsonTypeInfoResolver.Combine([.. Resolvers, new DefaultJsonTypeInfoResolver()]);
        }

        return built;
    }
}
