// <copyright file="MoreChangeableAggregate.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Support.Model
{
    using dddlib.Tests.Support.Events;

    public class MoreChangeableAggregate : ChangeableAggregate
    {
        public object OtherChange { get; private set; }

        private void Handle(SomethingHappened @event)
        {
            this.OtherChange = @event;
        }
    }
}
