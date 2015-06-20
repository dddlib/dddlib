// <copyright file="BadAggregate.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Support.Model
{
    public class BadAggregate : ChangeableAggregate
    {
        public object BadChange { get; private set; }

        private void Handle(int @event)
        {
            this.BadChange = @event;
        }

        private void Handle(int? @event)
        {
            this.BadChange = @event;
        }
    }
}
