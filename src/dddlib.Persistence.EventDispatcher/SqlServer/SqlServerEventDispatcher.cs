// <copyright file="SqlServerEventDispatcher.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.EventDispatcher.SqlServer
{
    using System;
    using dddlib.Persistence.EventDispatcher.Sdk;

    /// <summary>
    /// Represents the SQL Server event dispatcher service.
    /// </summary>
    public class SqlServerEventDispatcher : EventDispatcher
    {
        private SqlServerNotificationService notificationService;
        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerEventDispatcher"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="eventDispatcherDelegate">The event dispatcher delegate.</param>
        public SqlServerEventDispatcher(string connectionString, Action<long, object> eventDispatcherDelegate)
            : this(connectionString, new CustomEventDispatcher(eventDispatcherDelegate))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerEventDispatcher"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="eventDispatcher">The event dispatcher.</param>
        public SqlServerEventDispatcher(string connectionString, IEventDispatcher eventDispatcher)
             : this(eventDispatcher, new SqlServerEventStore(connectionString), new SqlServerNotificationService(connectionString))
        {
        }

        private SqlServerEventDispatcher(
            IEventDispatcher eventDispatcher,
            IEventStore eventStore,
            SqlServerNotificationService notificationService)
            : base(eventDispatcher, eventStore, notificationService, 50)
        {
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
                    this.notificationService.Dispose();
                }

                this.notificationService = null;

                this.isDisposed = true;
            }

            base.Dispose(disposing);
        }
    }
}
