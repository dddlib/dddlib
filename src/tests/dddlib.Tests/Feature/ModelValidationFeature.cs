// <copyright file="ModelValidationFeature.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Feature
{
    using System;
    using dddlib.Configuration;
    using dddlib.Tests.Sdk;
    using FluentAssertions;
    using Xbehave;

    // As someone who uses dddlib
    // In order to ensure my model is correctly implemented
    // I need to be able to validate the model
    public class ModelValidationFeature : Feature
    {
        /*  NOTE (Cameron): This feature is really odd, here's why:
            This originated from the need to provide a domain model designer with the ability to test their memento implementation. The oddness stems
            from the question about where this functionality should sit? I think it should be available from dddlib.dll itself especially if it is
            designed to help address other domain model validation (eg. whether a natural key has been specified). Needs more thought...  */

        public class ValidMementoImplementation : ModelValidationFeature
        {
            [Scenario]
            public void Scenario(Subject subject, Action action)
            {
                "Given an aggregate root with a valid memento implementation"
                    .f(() => subject = new Subject { Name = "name", Value = "value" });

                "When using the model assertions to validate the memento"
                    .f(() => action = () => ModelValidator.HasValidMemento(subject));

                "Then the action should not throw"
                    .f(() => action.ShouldNotThrow());
            }

            public class Subject : AggregateRoot
            {
                public string Name { get; set; }

                public string Value { get; set; }

                protected override object GetState()
                {
                    return new Memento
                    {
                        Name = this.Name,
                        Value = this.Value,
                    };
                }

                protected override void SetState(object memento)
                {
                    var subject = memento as Memento;

                    this.Name = subject.Name;
                    this.Value = subject.Value;
                }

                private class Memento
                {
                    public string Name { get; set; }
                    
                    public string Value { get; set; }
                }
            }

            private class BootStrapper : IBootstrap<Subject>
            {
                public void Bootstrap(IConfiguration configure)
                {
                    configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
                }
            }
        }

        public class InvalidMementoImplementation : ModelValidationFeature
        {
            [Scenario]
            public void Scenario(Subject subject, Action action)
            {
                "Given an aggregate root with a valid memento implementation"
                    .f(() => subject = new Subject { Name = "name", Value = "value" });

                "When using the model assertions to validate the memento"
                    .f(() => action = () => ModelValidator.HasValidMemento(subject));

                "Then the action should throw"
                    .f(() => action.ShouldThrow<Exception>());
            }

            public class Subject : AggregateRoot
            {
                public string Name { get; set; }

                public string Value { get; set; }

                protected override object GetState()
                {
                    return new Memento
                    {
                        Name = this.Name,
                        Value = this.Value,
                    };
                }

                protected override void SetState(object memento)
                {
                    var subject = memento as Memento;

                    this.Name = subject.Value;
                    this.Value = subject.Name;
                }

                private class Memento
                {
                    public string Name { get; set; }

                    public string Value { get; set; }
                }
            }

            private class BootStrapper : IBootstrap<Subject>
            {
                public void Bootstrap(IConfiguration configure)
                {
                    configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
                }
            }
        }
    }
}
