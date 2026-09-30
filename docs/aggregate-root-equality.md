# Aggregate Root Equality

In **dddlib** aggregate roots _are_ entities and, as such, use the underlying [entity equality](entity-equality.md)
implementation.

The only (optional) difference is when specifying the natural key of an aggregate root in the bootstrapper. In this
instance it is possible to use the `configure.AggregateRoot<T>()` method in place of `configure.Entity<T>()`, as
demonstrated below.

```csharp
internal sealed class Bootstrapper : dddlib.Configuration.IBootstrapper
{
    public void Bootstrap(dddlib.Configuration.IConfiguration configure)
    {
        configure.AggregateRoot<Car>()
            .ToUseNaturalKey(car => car.Registration);
    }
}
```

An aggregate root is saved and loaded by its natural key, so the analyzer reports one that has none, neither by
attribute in its class hierarchy nor in the bootstrapper (DDDLIB015).
