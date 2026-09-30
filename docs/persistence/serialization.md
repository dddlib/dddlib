# Serialization

Events, mementos and natural keys are serialized with System.Text.Json using the options in
`dddlib.Sdk.JsonSerialization.Options`: the default options, which write `DateTime` and `DateTimeOffset` as ISO 8601
round-trip strings. The same options are used by the in-memory and SQL Server implementations, so what works in a
test works in production.

## What must be serializable

- Events and mementos: plain classes with public settable properties, or a single public constructor whose parameters
  match the properties by name, which is what a positional record such as `record CarRegistered(string Registration)`
  has. They may be private nested types.
- Natural keys: `string`, `Guid`, numbers and other primitives serialize directly. A [value object](../value-objects.md)
  natural key serializes through its configured [value object serializer](../value-object-serialization.md), which is
  JSON of its public properties by default.

The analyzer reports a property of an event or memento that is saved but never loaded, because it has neither a
public setter nor a constructor parameter of the same name (DDDLIB012).

A natural key must round-trip: deserializing the serialized key must produce a value equal to the original. The
identity map checks this the first time it sees each aggregate root type and throws a `PersistenceException` if it
does not hold. The analyzer reports the natural key types it can tell will not round-trip (DDDLIB017): a class
compared by reference, and a value object that the default serializer cannot read back.

## Type names

Events, mementos and snapshots are stored with a stable name of their type, `Namespace.Type, AssemblyName`, without
version, culture or public key token. On load the name is resolved against the assemblies loaded in the process by
simple assembly name. A type that no longer exists is a `PersistenceException` naming the type and assembly.

## Source-generated contexts

To avoid reflection-based serialization (for trimming or ahead-of-time compilation, or for speed), write a
`JsonSerializerContext` for your events and mementos and register it before any persistence operation:

```csharp
[JsonSerializable(typeof(CarRegistered))]
[JsonSerializable(typeof(CarScrapped))]
internal partial class DomainJsonContext : JsonSerializerContext
{
}

dddlib.Sdk.JsonSerialization.AddTypeInfoResolver(DomainJsonContext.Default);
```

Registered contexts are consulted first; reflection remains the fallback for types they do not cover. Registration
after first use throws.
