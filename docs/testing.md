# Testing a Domain Model

The **dddlib.TestFramework** package exposes the persistence-facing surface of an aggregate root, which is internal
in **dddlib** itself, so that tests can assert on it.

```shell
dotnet add package dddlib.TestFramework
```

## Uncommitted events

`GetUncommittedEvents` returns the events an aggregate root has applied since it was created or last saved, in order.

```csharp
using dddlib.TestFramework;

var car = new Car("W807ASB");

var events = car.GetUncommittedEvents();
// events[0] is the CarRegistered event
```

An aggregate root only records events when it can be [reconstituted](aggregate-root-reconstitution.md); without a
reconstitution factory the list stays empty.

## Mementos and revisions

`GetMemento` calls the aggregate root's `GetState`, and `GetRevision` returns the number of events applied so far.

```csharp
var memento = car.GetMemento();
var revision = car.GetRevision();
```

## Validating a memento implementation

`ModelValidator.HasValidMemento` reconstitutes a second instance from the aggregate root's memento and checks that it
produces an identical memento. It throws `InvalidOperationException` when the aggregate root has no memento or when
`GetState` and `SetState` disagree, which is the usual bug (a property copied to the wrong place).

```csharp
using dddlib.TestFramework;

var car = new Car("W807ASB");

ModelValidator.HasValidMemento(car);
```

## In-memory persistence in tests

The in-memory repositories in **dddlib.Persistence** serialize through JSON exactly like SQL Server, so a persistence
test against `MemoryEventStoreRepository` or `MemoryRepository<T>` catches the same serialization mistakes without a
database. See [dddlib.Persistence](persistence/README.md).
