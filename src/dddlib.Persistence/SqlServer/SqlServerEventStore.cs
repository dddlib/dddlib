// <copyright file="SqlServerEventStore.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.SqlServer
{
    using System;
    using System.Collections.Generic;

    // TODO (Cameron): Make public. Implement.
    internal class SqlServerEventStore : IEventStore
    {
        public void Commit(Guid streamId, IEnumerable<object> events, string preCommitState, out string postCommitState)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<object> Get(Guid streamId, out string state)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<object> GetAll()
        {
            return this.GetEvents(0, null);
        }

        public IEnumerable<object> GetAll(IEnumerable<Type> eventTypes)
        {
            Guard.Against.NullOrEmptyOrNullElements(() => eventTypes);

            return this.GetEvents(0, eventTypes);
        }

        public IEnumerable<object> GetFrom(long eventId)
        {
            return this.GetEvents(eventId, null);
        }

        public IEnumerable<object> GetFrom(long eventId, IEnumerable<Type> eventTypes)
        {
            Guard.Against.NullOrEmptyOrNullElements(() => eventTypes);

            return this.GetEvents(eventId, eventTypes);
        }

        private IEnumerable<object> GetEvents(long eventId, IEnumerable<Type> eventTypes)
        {
            throw new NotImplementedException();
        }
    }
}
