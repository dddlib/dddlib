// <copyright file="AggregateRootLifecycle.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Feature
{
    using System;
    using dddlib.Tests.Sdk;
    using FluentAssertions;
    using Xbehave;

    // As someone who uses dddlib
    // In order to model destruction as a concept
    // I need to be able to end the lifecycle of an entity
    public class AggregateRootLifecycle : Feature
    {
        /*  TODO (Cameron):
            Needs to support PODO (plain old domain objects)  */

        [Scenario]
        public void CanDestroy(Action action)
        {
            var subject = default(Subject);

            "Given a subject"
                .f(() => subject = new Subject());

            "And the subject is updated"
                .f(() => subject.Update());

            "And the subject is destroyed"
                .f(() => subject.Destroy());

            "When the subject is updated again"
                .f(() => action = () => subject.Update());

            "Then that action should throw an exception"
                .f(() => action.ShouldThrow<dddlib.BusinessException>());
        }

        private class Subject : AggregateRoot
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

        private class SubjectUpdated
        {
            public int Version { get; set; }
        }
    }
}
