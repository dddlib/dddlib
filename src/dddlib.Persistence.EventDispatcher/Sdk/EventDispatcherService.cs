// <copyright file="EventDispatcherService.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.EventDispatcher.Sdk
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents an event dispatcher service.
    /// </summary>
    public class EventDispatcherService : IEventDispatcherService, IDisposable
    {
        private readonly object @lock = new object();
        private readonly Timer timer;

        private readonly IEventStore eventStore;
        private readonly IEventDispatcher eventDispatcher;
        private readonly INotificationService notificationService;
        private readonly int batchSize;

        private Batch bufferedBatch;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventDispatcherService"/> class.
        /// </summary>
        /// <param name="eventStore">The event store.</param>
        /// <param name="eventDispatcher">The event dispatcher.</param>
        /// <param name="notificationService">The notification service.</param>
        /// <param name="batchSize">Size of the batch.</param>
        public EventDispatcherService(
            IEventStore eventStore, 
            IEventDispatcher eventDispatcher,
            INotificationService notificationService, 
            int batchSize)
        {
            Guard.Against.Null(() => eventStore);
            Guard.Against.Null(() => eventDispatcher);
            Guard.Against.Null(() => notificationService);

            this.eventStore = eventStore;
            this.eventDispatcher = eventDispatcher;
            this.notificationService = notificationService;
            this.batchSize = batchSize;

            this.timer = new Timer(this.OnTimeout, null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Starts the service.
        /// </summary>
        public void Start()
        {
            this.notificationService.OnBatchPrepared += this.OnBatchPrepared;
            this.notificationService.OnEventCommitted += this.OnEventComitted;

            this.BufferNextBatch();
        }

        /// <summary>
        /// Stops the service.
        /// </summary>
        public void Stop()
        {
            this.notificationService.OnEventCommitted -= this.OnEventComitted;
            this.notificationService.OnBatchPrepared -= this.OnBatchPrepared;

            if (this.timer != null)
            {
                this.timer.Dispose();
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            this.Stop();
        }

        private void OnEventComitted(object sender, EventCommittedEventArgs e)
        {
            Console.WriteLine("Notify (Event Committed): {0}", e.EventId);

            this.BufferNextBatch();
        }

        private void OnBatchPrepared(object sender, BatchPrearedEventArgs e)
        {
            Console.WriteLine("Notify (Batch Prepared): {0}", e.BatchId);

            this.ProcessBatch(e.BatchId);
        }

        private void OnTimeout(object state)
        {
            Console.WriteLine("Buffer (Timeout)");

            this.RemoveFromBuffer();
        }

        private void BufferNextBatch()
        {
            lock (@lock)
            {
                if (this.bufferedBatch != null)
                {
                    return;
                }

                var batch = this.eventStore.GetNextUndispatchedEventsBatch(this.batchSize);
                if (batch == null)
                {
                    return;
                }

                Console.WriteLine("Buffer (Add): {0}", batch.Id);

                this.bufferedBatch = batch;

                this.timer.Change(30 * 1000, Timeout.Infinite);
            }
        }

        private bool TryRemoveFromBuffer(long batchId, out Batch batch)
        {
            lock (@lock)
            {
                if (this.bufferedBatch == null || this.bufferedBatch.Id != batchId)
                {
                    batch = null;
                    return false;
                }

                Console.WriteLine("Buffer (Remove): {0}", batchId);

                batch = this.bufferedBatch;

                this.RemoveFromBuffer();
            }

            return true;
        }

        private void RemoveFromBuffer()
        {
            this.bufferedBatch = null;

            this.timer.Change(Timeout.Infinite, Timeout.Infinite);

            // TODO (Cameron): This should be an async operation.
            Task.Factory.StartNew(() => this.BufferNextBatch());
        }

        // invoked while there is data in batches
        private void ProcessBatch(long batchId)
        {
            var batch = default(Batch);
            if (!this.TryRemoveFromBuffer(batchId, out batch))
            {
                ////lock (@lock)
                ////{
                ////    if (this.bufferedBatch != null && this.bufferedBatch.Id < batchId)
                ////    {
                ////        // NOTE (Cameron): This batch is stale.
                ////        this.RemoveFromBuffer();
                ////    }
                ////}

                return;
            }

            foreach (var @event in batch.Events)
            {
                this.eventDispatcher.Dispatch(@event.Id, @event.Payload);
                this.eventStore.MarkEventAsDispatched(@event.Id);
            }
        }
    }
}
