# dddlib

A domain driven design library for .NET designed to take the pain out of coding domain models. Support is provided
for [value objects](value-objects.md), [entities](entities.md), [aggregate roots](aggregate-roots.md) and
[business exceptions](business-exceptions.md). Domain models written following the [guidelines](guidelines.md)
can be packaged and distributed without any persistence-specific dependency, and support
[domain model reuse](concepts.md#domain-model-reuse) while remaining [persistence ignorant](concepts.md#persistence-ignorance).

**dddlib.Persistence** is the persistence companion, with **dddlib.Persistence.SqlServer** for SQL Server: in-memory
and SQL Server persistence for both
[memento-based](persistence/memento-persistence.md) and [event sourcing](persistence/event-sourcing-persistence.md)
models, with an [event dispatcher](persistence/event-dispatcher.md) and [projections](persistence/projections.md)
over the committed events.

## TL;DR

Install the package and write a model. Declaring the class `partial` lets the dddlib source generator produce the
event dispatch, natural key access and reconstitution code at compile time instead of discovering it by reflection.

```shell
dotnet add package dddlib
```

```csharp
public partial class Car : dddlib.AggregateRoot
{
    // used for reconstitution only
    protected Car()
    {
    }

    public Car(string registration)
    {
        // business logic goes here...
        this.Apply(new CarRegistered { Registration = registration });
    }

    [dddlib.NaturalKey]
    public string? Registration { get; private set; }

    private void Handle(CarRegistered @event)
    {
        this.Registration = @event.Registration;
    }
}

public class CarRegistered
{
    public string? Registration { get; set; }
}
```

Persist it. The schema is created or upgraded explicitly, here at startup (see [SQL Server](persistence/sql-server.md)).

```shell
dotnet add package dddlib.Persistence.SqlServer
```

```csharp
// replace with a valid SQL Server connection string
var connectionString = "Server=someServer;Database=someDatabase;";
await dddlib.Persistence.SqlServer.SqlServerSchema.EnsureAsync(connectionString);

var repository = new dddlib.Persistence.SqlServer.SqlServerEventStoreRepository(connectionString);

var car = new Car("W807ASB");
await repository.SaveAsync(car);

var sameCar = await repository.LoadAsync<Car>(car.Registration!);
```

## Contents

Overview

- [Quickstart](quickstart.md)
- [Quickstart (Persistence)](quickstart-persistence.md)
- [Guidelines](guidelines.md)
- [Concepts](concepts.md)
- [Migrating from v1](migrating-from-v1.md)

dddlib

- [Aggregate Roots](aggregate-roots.md)
  - [Equality](aggregate-root-equality.md)
  - [Lifecycle Management](aggregate-root-lifecycle-management.md)
  - [Event Application](aggregate-root-event-application.md)
  - [Reconstitution](aggregate-root-reconstitution.md)
  - [Mementos](aggregate-root-mementos.md)
- [Entities](entities.md)
  - [Equality](entity-equality.md)
  - [Lifecycle Management](entity-lifecycle-management.md)
- [Value Objects](value-objects.md)
  - [Equality](value-object-equality.md)
  - [Serialization](value-object-serialization.md)
- [Services](services.md)
- [Bootstrapper](bootstrapper.md)
- [Business Exceptions](business-exceptions.md)
- [Source Generator and Analyzers](source-generator.md)
- [Testing a Domain Model](testing.md) (dddlib.TestFramework)

dddlib.Persistence

- [Overview](persistence/README.md)
- [Memento Persistence](persistence/memento-persistence.md) (in-memory and SQL Server)
- [Event Sourcing Persistence](persistence/event-sourcing-persistence.md) (in-memory, SQL Server, snapshotting)
- [SQL Server](persistence/sql-server.md)
- [Serialization](persistence/serialization.md)
- [Event Dispatcher](persistence/event-dispatcher.md) (dddlib.Persistence.EventDispatcher)
- [Projections](persistence/projections.md) (dddlib.Persistence.Projections)
