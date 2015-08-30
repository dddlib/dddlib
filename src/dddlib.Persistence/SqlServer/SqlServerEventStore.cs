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
        public void CommitStream(Guid streamId, IEnumerable<object> events, string preCommitState, out string postCommitState)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<object> GetStream(Guid streamId, out string state)
        {
            throw new NotImplementedException();
        }
    }
}
