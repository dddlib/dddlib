# Quickstart

> There is no "quick start" to modelling a domain: every model is specific to the bounded context within which the
> domain is being modelled. What follows are the basics of using **dddlib** to write a model for your domain.

1. Install the **dddlib** package into your project:

   ```shell
   dotnet add package dddlib
   ```

   The package includes a source generator and analyzers. They need nothing from you beyond declaring your domain
   types `partial`; see [Source Generator and Analyzers](source-generator.md).

2. Create your domain model. For best results follow the [guidelines](guidelines.md).
   _The code below is an example only and does not satisfy the requirements for persistence. For an example that does,
   see the [persistence quickstart](quickstart-persistence.md)._

   ```csharp
   public partial class Car : dddlib.AggregateRoot
   {
       public Car(string registration)
       {
           // business logic goes here...
           this.Registration = registration;
       }

       [dddlib.NaturalKey]
       public string Registration { get; private set; }
   }
   ```

3. Optionally, configure your domain model by creating a [bootstrapper](bootstrapper.md) class that implements
   `IBootstrapper`. Again, for best results follow the [guidelines](guidelines.md).

   ```csharp
   internal sealed class Bootstrapper : dddlib.Configuration.IBootstrapper
   {
       public void Bootstrap(dddlib.Configuration.IConfiguration configure)
       {
           // configuration goes here...
           configure.AggregateRoot<Car>().ToUseNaturalKey(car => car.Registration);
       }
   }
   ```

4. Use your domain model.

   ```csharp
   var car = new Car("W807 ASB");
   var sameCar = new Car("W807 ASB");

   // trivial example of usage...
   var areEqual = car == sameCar;
   ```

5. Optionally, [persist](quickstart-persistence.md) your [aggregate roots](aggregate-roots.md) once they have been
   created.
