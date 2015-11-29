// <copyright file="AggregateRootExtensions.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Sdk
{
    using System.Collections.Generic;

    public static class AggregateRootExtensions
    {
        public static IEnumerable<object> GetUncommittedEvents(this AggregateRoot aggregateRoot)
        {
            return aggregateRoot.GetUncommittedEvents();
        }

        public static object GetMemento(this AggregateRoot aggregateRoot)
        {
            return aggregateRoot.GetMemento();
        }

        public static int GetRevision(this AggregateRoot aggregateRoot)
        {
            return aggregateRoot.Revision;
        }
    }
}
