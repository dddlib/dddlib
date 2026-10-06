# Concepts

One of the driving factors behind the design of **dddlib** is a domain model that is entirely independent of the
persistence layer. Any domain model created with this library that follows the [guidelines](guidelines.md) may be
packaged and shipped in isolation. These models automatically support domain model reuse and persistence ignorance.

## Domain Model Reuse

On occasion it may be desirable to extend some part of an existing domain model. **dddlib** gives the original model
author the ability to customize every aspect of the model that is required to facilitate reuse. Ultimately the model
author is creating a class library containing classes that may be inherited, but there are concerns beyond those of
ordinary inheritance, specifically relating to persistence. For that reason entities and aggregate roots should not
be `sealed`; the analyzers report one that is (DDDLIB025). The topics below detail how to customize the model for
reuse:

- [Aggregate Root Reconstitution](aggregate-root-reconstitution.md)
- [Aggregate Root Mementos](aggregate-root-mementos.md)
- [Value Object Serialization](value-object-serialization.md)

## Persistence Ignorance

Model authors should not have to consider the implementation-specific concerns of the persistence layer. However,
some persistence concerns are inherently specific to the model.

For example, in many repository implementations the equality operations used for identity comparison are performed in
the persistence layer. That makes no sense in the context of a domain model where the natural key of an aggregate may
be a [value object](value-objects.md) with a [custom equality comparer](value-object-equality.md#custom-value-object-equality).
In **dddlib** all equality operations, including identity comparison, are persistence independent.

## Runtime and Compile Time

At runtime **dddlib** keeps a registry of metadata about each domain type (`dddlib.Runtime.Application`): the natural
key, the event handlers, the reconstitution factory and the value object comparer and serializer. The metadata is
built once per type, from the [bootstrapper](bootstrapper.md) and either the code the
[source generator](source-generator.md) emitted for `partial` types or reflection for the rest. Mistakes in the
model that the runtime would report as a `RuntimeException` are reported by the analyzers at compile time when they
can be detected statically.
