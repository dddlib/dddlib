// <copyright file="INotificationService.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.EventDispatcher.Sdk
{
    using System;

    /// <summary>
    /// Exposes the public members of the notification service.
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// Occurs when an event is committed.
        /// </summary>
        event EventHandler<EventCommittedEventArgs> OnEventCommitted;

        /// <summary>
        /// Occurs when a batch is prepared.
        /// </summary>
        event EventHandler<BatchPreparedEventArgs> OnBatchPrepared;
    }
}
