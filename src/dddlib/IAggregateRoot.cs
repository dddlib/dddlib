// <copyright file="IAggregateRoot.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib
{
    using System.Collections.Generic;

    internal interface IAggregateRoot
    {
        string State { get; }

        void Initialize(object memento, IEnumerable<object> events, string state);

        object GetMemento();

        IEnumerable<object> GetUncommittedEvents();

        void CommitEvents(string state);
    }
}
