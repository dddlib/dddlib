namespace dddlib.Tests.Bug;

// https://github.com/dddlib/dddlib/issues/92
// Ending the lifecycle of an aggregate root twice must throw a business exception.
public class Bug0092
{
    [Test]
    public async Task RespectLifecycle()
    {
        var subject = new Subject();
        subject.EndLifecycle();

        var action = subject.EndLifecycle;

        await Assert.That(action).Throws<BusinessException>();
    }

    public class Subject : AggregateRoot
    {
        public new void EndLifecycle() => base.EndLifecycle();
    }
}
