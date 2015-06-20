// <copyright file="AggregateRoot.Configuration.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib
{
    using dddlib.Runtime;
    using dddlib.Sdk.Configuration.Model;

    /// <content>
    /// Represents an aggregate root.
    /// </content>
    public abstract partial class AggregateRoot
    {
        internal AggregateRoot(IEventDispatcher eventDispatcher, bool persistEvents)
            : this(@this => new TypeInformation(eventDispatcher, persistEvents))
        {
        }

        private class TypeInformation
        {
            public TypeInformation(AggregateRootType aggregateRootType)
            {
                Guard.Against.Null(() => aggregateRootType);

                this.EventDispatcher = aggregateRootType.EventDispatcher;
                this.PersistEvents = aggregateRootType.PersistEvents;
            }

            public TypeInformation(IEventDispatcher eventDispatcher, bool persistEvents)
            {
                this.EventDispatcher = eventDispatcher;
                this.PersistEvents = persistEvents;
            }

            public IEventDispatcher EventDispatcher { get; set; }

            public bool PersistEvents { get; set; }
        }
    }
}
