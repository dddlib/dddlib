using dddlib.Tests.Support;

namespace dddlib.Tests.Features.Generated;

// As someone who uses dddlib
// In order to stop changes being applied to an aggregate root that no longer exists
// I need to be able to end the lifecycle of an aggregate root
public abstract partial class AggregateRootLifecycleManagement : Feature
{
    public sealed partial class DefaultLifecycle : AggregateRootLifecycleManagement
    {
        [Test]
        public async Task Scenario()
        {
            // Given a subject
            var subject = new Subject();

            // And the subject is updated
            subject.Update();

            // And the subject is destroyed
            subject.Destroy();

            // When the subject is updated again
            var action = subject.Update;

            // Then that action should throw an exception
            await Assert.That(action).Throws<dddlib.BusinessException>();
        }

        private partial class Subject : AggregateRoot
        {
            private int version;

            public void Update()
            {
                if (this.IsDestroyed)
                {
                    throw new dddlib.BusinessException("Lifecycle has ended!");
                }

                this.version++;
            }

            public void Destroy()
            {
                this.EndLifecycle();
            }
        }
    }

    public sealed partial class EventBasedLifecycle : AggregateRootLifecycleManagement
    {
        [Test]
        public async Task Scenario()
        {
            // Given a subject
            var subject = new Subject();

            // And the subject is updated
            subject.Update();

            // And the subject is destroyed
            subject.Destroy();

            // When the subject is updated again
            var action = subject.Update;

            // Then that action should throw an exception
            await Assert.That(action).Throws<dddlib.BusinessException>();
        }

        private partial class Subject : AggregateRoot
        {
            private int version;

            public void Update()
            {
                this.Apply(new SubjectUpdated { Version = this.version + 1 });
            }

            public void Destroy()
            {
                this.EndLifecycle();
            }

            private void Handle(SubjectUpdated @event)
            {
                this.version = @event.Version;
            }
        }

        private sealed partial class SubjectUpdated
        {
            public int Version { get; set; }
        }
    }
}
