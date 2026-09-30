# Source Generator and Analyzers

The **dddlib** package ships a Roslyn source generator and analyzers. They run inside the compiler; nothing is needed
at runtime and there is no second package to install.

## Generated code

For every aggregate root, entity and value object declared `partial` (including all of its containing types), the
generator emits a private nested class holding:

- exact-type event dispatch over the `Handle` methods the type declares;
- the natural key accessor for the property marked `[NaturalKey]`;
- a reconstitution factory, when the type has a parameterless constructor;
- an equality comparer over the public properties, for value objects.

The runtime finds this class once per type and uses it on the hot paths. Types that are not `partial` work
identically through reflection, so a model can be migrated one type at a time and a non-partial subclass of a partial
aggregate root works. Dispatch is composed one level of the class hierarchy at a time.

The generator also registers the assembly's [bootstrapper](bootstrapper.md) at module initialization, replacing the
assembly scan.

What the generator does not do: it cannot produce a `System.Text.Json` serializer context for your events, because
Roslyn generators do not see each other's output. You can write one yourself and register it; see
[serialization](persistence/serialization.md).

## Diagnostics

| Id | Severity | Reported when |
|---|---|---|
| DDDLIB001 | Error | An entity or aggregate root declares more than one `[NaturalKey]` property |
| DDDLIB002 | Warning | A `Handle` method takes a value type; events must be classes, so it would never be called |
| DDDLIB003 | Warning | A `Handle` method is public; only non-public handlers are dispatched to |
| DDDLIB004 | Warning | A value object has no public properties, so no two instances could be equal under the default comparer |
| DDDLIB005 | Error | An assembly declares more than one bootstrapper |
| DDDLIB006 | Error | A bootstrapper has no public parameterless constructor |
| DDDLIB007 | Info | A domain type (or one of its containing types) is not `partial` and could be |
| DDDLIB008 | Warning | An aggregate root applies an event that no `Handle` method in its class hierarchy takes, so applying it changes no state |
| DDDLIB009 | Warning | A `Handle` method takes an abstract class or an interface; dispatch is by exact type, so it would never be called |
| DDDLIB010 | Warning | A `Handle` method applies an event; handlers run again on load, where the event would be recorded again |
| DDDLIB011 | Warning | A `Handle` method throws; handlers run again on load, which must not fail |
| DDDLIB012 | Warning | An event or memento has a property with no public setter and no constructor parameter of the same name, so it is saved but never loaded |
| DDDLIB013 | Warning | An aggregate root overrides only one of `GetState` and `SetState` |
| DDDLIB014 | Warning | An aggregate root has no parameterless constructor and the bootstrapper does not call `ToReconstituteUsing` for it |
| DDDLIB015 | Warning | An aggregate root has no `[NaturalKey]` in its class hierarchy and the bootstrapper does not call `ToUseNaturalKey` for it |
| DDDLIB016 | Error | `[NaturalKey]` is on a property that is ignored: not public, static, an indexer, without a getter, or not on an entity |
| DDDLIB017 | Warning | The natural key of an aggregate root is a class compared by reference, or a value object that the default serializer cannot read back and that has no serializer configured |
| DDDLIB018 | Error | A value object derives from `ValueObject<T>` of a type other than itself |
| DDDLIB019 | Warning | A public property of a value object is a class compared by reference, so equal content does not make equal value objects |
| DDDLIB020 | Warning | `Map` is used to convert to or from an event for which the bootstrapper configures no mapping (or no reverse mapping) |
| DDDLIB021 | Error | The bootstrapper's `ToUseNaturalKey` selects a different property from the one marked `[NaturalKey]` on the same type |
| DDDLIB022 | Error | A `ToUseNaturalKey` selector is not a property of its parameter |

DDDLIB004 is not reported for a value object that the bootstrapper configures a comparer for.

## Requirements

The generator targets the Roslyn version shipped with the .NET 9.0.300 SDK and Visual Studio 17.14, and loads in any
later compiler. Generic domain types are not generated for.
