using dddlib.Configuration;
using dddlib.TestFramework;
using dddlib.Tests.Support;

namespace dddlib.Tests.Features.Generated;

// As someone who designs a domain model with dddlib
// In order to trust my memento implementation
// I need to be able to validate that a memento round-trips
public abstract partial class ModelValidationFeature : Feature
{
    public sealed partial class ValidMementoImplementation : ModelValidationFeature
    {
        [Test]
        public async Task Scenario()
        {
            // Given an aggregate root with a valid memento implementation
            var subject = new Subject { Name = "name", Value = "value" };

            // When using the model assertions to validate the memento
            var action = () => ModelValidator.HasValidMemento(subject);

            // Then the action should not throw
            await Assert.That(action).ThrowsNothing();
        }

        public partial class Subject : AggregateRoot
        {
            public string? Name { get; set; }

            public string? Value { get; set; }

            protected override object GetState() => new Memento { Name = this.Name, Value = this.Value };

            protected override void SetState(object memento)
            {
                var subject = (Memento)memento;
                this.Name = subject.Name;
                this.Value = subject.Value;
            }

            private sealed partial class Memento
            {
                public string? Name { get; set; }

                public string? Value { get; set; }
            }
        }

        private sealed partial class BootStrapper : IBootstrap<Subject>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
            }
        }
    }

    public sealed partial class InvalidMementoImplementation : ModelValidationFeature
    {
        [Test]
        public async Task Scenario()
        {
            // Given an aggregate root with an invalid memento implementation
            var subject = new Subject { Name = "name", Value = "value" };

            // When using the model assertions to validate the memento
            var action = () => ModelValidator.HasValidMemento(subject);

            // Then the action should throw
            await Assert.That(action).Throws<InvalidOperationException>();
        }

        public partial class Subject : AggregateRoot
        {
            public string? Name { get; set; }

            public string? Value { get; set; }

            protected override object GetState() => new Memento { Name = this.Name, Value = this.Value };

            protected override void SetState(object memento)
            {
                var subject = (Memento)memento;
                this.Name = subject.Value;
                this.Value = subject.Name;
            }

            private sealed partial class Memento
            {
                public string? Name { get; set; }

                public string? Value { get; set; }
            }
        }

        private sealed partial class BootStrapper : IBootstrap<Subject>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
            }
        }
    }
}
