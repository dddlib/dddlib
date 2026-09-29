using System.Globalization;
using dddlib.Runtime;

namespace dddlib.Sdk;

/// <summary>
/// Creates aggregate roots from persisted state: an optional memento and the events that follow it.
/// </summary>
internal static class AggregateRootFactory
{
    public static T Create<T>(object? memento, int revision, IEnumerable<object> events, string? state)
        where T : AggregateRoot
    {
        ArgumentNullException.ThrowIfNull(events);

        var runtimeType = Application.Current.GetAggregateRootType(typeof(T));
        if (runtimeType.UninitializedFactory is not Func<T> uninitializedFactory)
        {
            throw new RuntimeException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    @"The aggregate root of type '{0}' does not have a factory method for reconstitution registered with the runtime.
To fix this issue, either:
- use a bootstrapper to configure reconstitution for that aggregate root, or
- add a protected internal default constructor to the aggregate root.",
                    typeof(T)))
            {
                HelpLink = "https://github.com/dddlib/dddlib/wiki/Aggregate-Root-Reconstitution",
            };
        }

        var aggregateRoot = uninitializedFactory();
        aggregateRoot.Initialize(memento, revision, events, state);

        return aggregateRoot;
    }
}
