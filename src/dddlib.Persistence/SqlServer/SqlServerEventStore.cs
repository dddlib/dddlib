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
        public void CommitStream(Guid id, IEnumerable<object> events, string preCommitState, out string postCommitState)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<object> GetStream(Guid id, out string state)
        {
            throw new NotImplementedException();
        }

        public void ReplayEventsTo(params object[] views)
        {
            throw new NotImplementedException();
        }
    }
}
