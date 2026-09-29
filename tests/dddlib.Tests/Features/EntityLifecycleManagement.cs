using dddlib.Tests.Support;

namespace dddlib.Tests.Features;

// As someone who uses dddlib
// In order to stop changes being applied to an entity that no longer exists
// I need to be able to end the lifecycle of an entity
public abstract class EntityLifecycleManagement : Feature
{
    public sealed class EntityLifecycle : EntityLifecycleManagement
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

        private sealed class Subject : Entity
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
}
