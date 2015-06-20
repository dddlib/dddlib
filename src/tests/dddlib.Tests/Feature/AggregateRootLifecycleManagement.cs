// <copyright file="AggregateRootLifecycleManagement.cs" company="dddlib contributors">
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
    public class AggregateRootLifecycleManagement : Feature
    {
        /*  TODO (Cameron): Split into Entity lifecycle management.  */

        public class DefaultLifecycle : AggregateRootLifecycleManagement
        {
            [Scenario]
            public void Scenario(Action action)
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
                    // NOTE (Cameron): We have to do the heavy lifting ourselves in this instance.
                    if (this.IsDestroyed)
                    {
                        throw new dddlib.BusinessException("Lifecycle has ended!");
                    }

                    this.version = this.version + 1;
                }

                public void Destroy()
                {
                    this.EndLifecycle();
                }
            }

            private class SubjectUpdated
            {
                public int Version { get; set; }
            }
        }

        public class EventBasedLifecycle : AggregateRootLifecycleManagement
        {
            [Scenario]
            public void Scenario(Action action)
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
}
