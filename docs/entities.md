# Entities

> Many objects are not fundamentally defined by their attributes, but rather by a thread of continuity and identity.
> _Eric Evans, Domain-driven Design: Tackling Complexity in the Heart of Software_

In **dddlib** the following features are supported:

- [Entity equality](entity-equality.md) enables the author to specify the natural key of an entity, which is then
  used for equality operations.
- [Entity lifecycle management](entity-lifecycle-management.md) enables the author to control the behaviour of an
  entity based on whether its lifecycle has ended.
