using System.Text.Json;
using dddlib.Sdk;

namespace dddlib.Tests.Support;

/// <summary>
/// Assertions that help a domain model designer validate their model.
/// </summary>
public static class ModelValidator
{

    /// <summary>
    /// Validates that the memento produced by the aggregate root round-trips: reconstituting a new instance from the
    /// memento produces an identical memento.
    /// </summary>
    /// <exception cref="InvalidOperationException">The aggregate root has no memento, or the memento does not round-trip.</exception>
    public static void HasValidMemento<T>(T aggregate)
        where T : AggregateRoot
    {
        ArgumentNullException.ThrowIfNull(aggregate);

        var memento = aggregate.GetMemento() ?? throw new InvalidOperationException("No memento defined!");

        var sameAggregate = AggregateRootFactory.Create<T>(memento, aggregate.Revision, [], "test");
        var sameMemento = sameAggregate.GetMemento();

        var expected = JsonSerializer.Serialize(memento, memento.GetType());
        var actual = sameMemento is null ? null : JsonSerializer.Serialize(sameMemento, sameMemento.GetType());

        if (actual != expected)
        {
            throw new InvalidOperationException("Invalid memento implementation!");
        }
    }
}
