# Quickstart (Persistence)

1. Install the **dddlib.Persistence** package into your project, and **dddlib.Persistence.SqlServer** for SQL Server:

   ```shell
   dotnet add package dddlib.Persistence
   dotnet add package dddlib.Persistence.SqlServer
   ```

2. Create your domain model. For best results follow the [guidelines](guidelines.md).

   ```csharp
   public partial class Car : dddlib.AggregateRoot
   {
       // used for reconstitution only
       protected internal Car()
       {
       }

       public Car(string registration)
       {
           // business logic goes here...
           this.Apply(new CarRegistered { Registration = registration });
       }

       [dddlib.NaturalKey]
       public string? Registration { get; private set; }

       // used for memento reconstitution or snapshot loading
       protected override object? GetState()
       {
           return new Memento { Registration = this.Registration };
       }

       // used for memento persistence or snapshot saving
       protected override void SetState(object memento)
       {
           var car = (Memento)memento;
           this.Registration = car.Registration;
       }

       private void Handle(CarRegistered @event)
       {
           this.Registration = @event.Registration;
       }

       // an object used to describe the state of the aggregate root
       private sealed class Memento
       {
           public string? Registration { get; set; }
       }
   }

   public class CarRegistered
   {
       public string? Registration { get; set; }
   }
   ```

3. Choose which persistence mechanism to use to persist and reconstitute an [aggregate root](aggregate-roots.md).
   Both repositories are asynchronous.

   To use [memento persistence](persistence/memento-persistence.md):

   ```csharp
   // for SQL Server, use: new dddlib.Persistence.SqlServer.SqlServerMementoRepository<Car>(connectionString)
   var repository = new dddlib.Persistence.Memory.MemoryRepository<Car>();

   var car = new Car("W807ASB");
   await repository.SaveAsync(car);

   var sameCar = await repository.LoadAsync(car.Registration!);
   ```

   To use [event sourcing persistence](persistence/event-sourcing-persistence.md):

   ```csharp
   // for SQL Server, use: new dddlib.Persistence.SqlServer.SqlServerEventStoreRepository(connectionString)
   var repository = new dddlib.Persistence.Memory.MemoryEventStoreRepository();

   var car = new Car("W807ASB");
   await repository.SaveAsync(car);

   var sameCar = await repository.LoadAsync<Car>(car.Registration!);
   ```

4. For SQL Server, create the schema before first use with `await SqlServerSchema.EnsureAsync(connectionString)`,
   from a migration step or at startup. See [SQL Server](persistence/sql-server.md).
