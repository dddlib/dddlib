# Guidelines

- To create an [aggregate root](aggregate-roots.md), create a class that inherits from `AggregateRoot`:

  ```csharp
  public partial class MyAggregateRoot : dddlib.AggregateRoot
  {
  }
  ```

- To create an [entity](entities.md), create a class that inherits from `Entity`:

  ```csharp
  public partial class SomeOtherThing : dddlib.Entity
  {
  }
  ```

- To create a [value object](value-objects.md), create a class that inherits from `ValueObject<T>`:

  ```csharp
  public sealed partial class SomeValue : dddlib.ValueObject<SomeValue>
  {
  }
  ```

- Declare domain types, and the types that contain them, `partial`. The [source generator](source-generator.md) then
  emits event dispatch, natural key access, reconstitution and value object equality at compile time; types that are
  not `partial` work the same way through reflection.

- Do not seal an aggregate root or an entity. They are designed for inheritance, so that a domain model can be reused
  and extended in another assembly. Value objects, by contrast, are usually best sealed.

- Give an aggregate root a `protected` parameterless constructor so it can be
  [reconstituted](aggregate-root-reconstitution.md) and so derived aggregate roots can chain to it from their own.

- Keep event classes plain: public settable properties, no behaviour, and no dependency on the aggregate root. They
  are serialized as JSON by the persistence layer.

- Throw a [business exception](business-exceptions.md) when business logic criteria are not met, with a message a
  business user would understand.

- Put configuration that cannot be expressed in the model itself in a single [bootstrapper](bootstrapper.md) per
  assembly.
