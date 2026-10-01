using dddlib.Persistence.Projections.Sdk;

namespace dddlib.Persistence.Projections;

/// <summary>
/// A projection into key/value views: handlers receive the event and the views to write. Subclass it, give it a name
/// and register the handlers in the constructor:
/// <code>
/// public sealed class CarProjection : Projection&lt;string, CarView&gt;
/// {
///     public CarProjection()
///         : base("cars")
///     {
///         this.When&lt;CarRegistered&gt;((e, views) => views.AddOrUpdateAsync(e.Registration, new CarView(e.Registration)));
///     }
/// }
/// </code>
/// Run it with a <see cref="ProjectionRunner"/> over a store for the views: in memory,
/// <see cref="Memory.MemoryProjectionStore{TIdentity, TEntity}"/>.
/// </summary>
/// <typeparam name="TIdentity">The type of the identity a view is keyed by.</typeparam>
/// <typeparam name="TEntity">The type of the view.</typeparam>
public abstract class Projection<TIdentity, TEntity> : ProjectionBase<IRepository<TIdentity, TEntity>>
    where TIdentity : notnull
    where TEntity : class
{
    protected Projection(string name)
        : base(name)
    {
    }
}
