// <copyright file="MemoryEventDispatcher.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.EventDispatcher.Memory
{
    using System;
    using dddlib.Persistence.EventDispatcher.Sdk;

    /// <summary>
    /// Represents the memory event dispatcher service.
    /// </summary>
    public class MemoryEventDispatcher : EventDispatcher
    {
        private readonly MemoryEventStore eventStore;
        private readonly MemoryNotificationService notificationService;

        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryEventDispatcher"/> class.
        /// </summary>
        /// <param name="eventDispatcherDelegate">The event dispatcher delegate.</param>
        public MemoryEventDispatcher(Action<long, object> eventDispatcherDelegate)
            : this(new CustomEventDispatcher(eventDispatcherDelegate))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryEventDispatcher"/> class.
        /// </summary>
        /// <param name="eventDispatcher">The event dispatcher.</param>
        public MemoryEventDispatcher(IEventDispatcher eventDispatcher)
            : this(eventDispatcher, new MemoryEventStore(), new MemoryNotificationService())
        {
        }

        private MemoryEventDispatcher(
            IEventDispatcher eventDispatcher,
            MemoryEventStore eventStore,
            MemoryNotificationService notificationService)
            : base(eventDispatcher, eventStore, notificationService, 50)
        {
            this.eventStore = eventStore;
            this.notificationService = notificationService;
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing">Set to <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (!this.isDisposed)
            {
                if (disposing)
                {
                    this.eventStore.Dispose();
                    this.notificationService.Dispose();
                }

                this.isDisposed = true;
            }

            base.Dispose(disposing);
        }
    }
}
