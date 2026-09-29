# Entity Lifecycle Management

In **dddlib** responsibility for managing an entity's lifecycle lies with the model author. However, the base class
can record the end of an entity's lifecycle.

Below is a car model that makes use of the underlying entity lifecycle functionality. Calling `this.EndLifecycle()`
sets `this.IsDestroyed` to `true`, which can then be used explicitly in other methods. Calling `EndLifecycle` a second
time throws a [business exception](business-exceptions.md).

```csharp
public partial class Car : dddlib.Entity
{
    public Car(string registration)
    {
        this.Registration = registration;
    }

    [dddlib.NaturalKey]
    public string Registration { get; private set; }

    public void Scrap()
    {
        // the author of the model is responsible for this logic...
        if (this.IsDestroyed)
        {
            throw new dddlib.BusinessException(
                "Cannot scrap a car that has already been scrapped!");
        }

        // this call sets the value of 'this.IsDestroyed' to 'true'
        this.EndLifecycle();
    }
}
```

`ThrowIfLifecycleEnded()` is also available to guard any method with the standard message.
