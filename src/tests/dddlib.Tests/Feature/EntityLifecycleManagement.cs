// <copyright file="EntityLifecycleManagement.cs" company="dddlib contributors">
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
    public class EntityLifecycleManagement : Feature
    {
        public class EntityLifecycle : EntityLifecycleManagement
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

            private class Subject : Entity
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
        }
    }
}
