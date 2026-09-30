# Value Objects

> Many objects have no conceptual identity. These objects describe some characteristic of a thing.
> _Eric Evans, Domain-driven Design: Tackling Complexity in the Heart of Software_

In **dddlib** the following features are supported:

- [Value object equality](value-object-equality.md) enables the author to use the default value object equality or,
  where that is insufficient, to define a custom equality comparer.
- [Value object serialization](value-object-serialization.md) enables the author to control the serialization of a
  value object for persistence.

A value object derives from `ValueObject<T>` of itself: `class Money : ValueObject<Money>`. Any other type argument
is reported by the analyzer (DDDLIB018).

## Why a base class and not a C# record

C# records were considered for v2 and rejected. Record equality includes every instance field, private ones included,
which is the wrong semantics for a value object: a cached or derived private field would break equality.
`ValueObject<T>` compares the public properties only, which is what a value object means. Records are not accepted
where a value object is expected.
