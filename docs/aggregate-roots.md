# Aggregate Roots

> Aggregates mark off the scope within which invariants have to be maintained at every stage of the lifecycle.
> _Eric Evans, Domain-driven Design: Tackling Complexity in the Heart of Software_

In **dddlib** the following features are supported:

- [Aggregate root equality](aggregate-root-equality.md) enables the author to specify the natural key of an
  aggregate root, which is then used for equality operations.
- [Aggregate root lifecycle management](aggregate-root-lifecycle-management.md) enables the author to control the
  behaviour of an aggregate root based on whether its lifecycle has ended.
- [Aggregate root event application](aggregate-root-event-application.md) enables the author to design the model
  using an event based approach, which in turn opens up [event sourcing persistence](persistence/event-sourcing-persistence.md).
- [Aggregate root reconstitution](aggregate-root-reconstitution.md) enables the author to specify the uninitialized
  factory used to reconstitute the aggregate root.
- [Aggregate root mementos](aggregate-root-mementos.md) enables the author to control the representation of the
  aggregate root that is passed to the persistence layer.
