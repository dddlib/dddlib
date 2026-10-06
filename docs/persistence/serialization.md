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
public setter nor a constructor parameter of the same name (DDDLIB012). It also reports an event or memento that is
saved but fails to load (DDDLIB023): one with several public constructors and none of them parameterless or marked
`[JsonConstructor]`, one with no public constructor, and one with a constructor parameter that no property matches.
Saving does not use the constructor, so without the analyzer the first sign is an exception when the aggregate root
is loaded.

A natural key must round-trip: deserializing the serialized key must produce a value equal to the original. The
identity map checks this the first time it sees each aggregate root type and throws a `PersistenceException` if it
does not hold. The analyzer reports the natural key types it can tell will not round-trip (DDDLIB017): a class
compared by reference, and a value object that the default serializer cannot read back.

## How natural keys are compared

The identity map must not give two equal natural keys two identities. How it checks depends on whether equal keys of
the type always serialize to the same text, and unequal keys to different text:

- **Compared by the repository.** For a `string`, an integer, a `Guid`, a `bool`, a `char`, an enumeration, a nullable
  one of those, or a sealed [value object](../value-objects.md) with the default equality comparer and serializer whose
  properties are all of these types (without `[JsonIgnore]` or `[JsonConverter]`), comparing the serialized keys is the
  same as comparing the keys. The repository finds or adds the key in a single call, so writers of different keys of
  the same aggregate root type never make each other retry. On SQL Server a unique index keeps two writers of the
  same key from both adding it.
- **Compared by the identity map.** For any other key it compares with the key's own equality: a value object with a
  custom equality comparer, such as a case-insensitive one, or a custom serializer, an unsealed value object, and
  types whose equal values can serialize differently, such as `decimal` (`1.0m` and `1.00m`), `double`, `DateTime`
  and `DateTimeOffset`. It synchronizes with every key added for the aggregate root type and adds the key only if no
  other key was added meanwhile, retrying otherwise. Many concurrent writers of new keys for one type therefore wait
  on each other, and one may eventually fail with a `PersistenceException`.

`DefaultNaturalKeySerializer.IsCanonical` makes this decision. A custom `INaturalKeySerializer` uses the identity
map's comparison unless it implements `IsCanonical`, and a custom natural key repository is used for the repository's
comparison only if it implements `IUniqueNaturalKeyRepository`.

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
