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
        private readonly SqlServerNotificationService notificationService;

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
        /// <param name="eventDispatcherDelegate">The event dispatcher delegate.</param>
        /// <param name="dispatcherId">The dispatcher identifier.</param>
        public SqlServerEventDispatcher(string connectionString, Action<long, object> eventDispatcherDelegate, string dispatcherId)
            : this(connectionString, new CustomEventDispatcher(eventDispatcherDelegate), dispatcherId)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerEventDispatcher"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="schema">The schema.</param>
        /// <param name="eventDispatcherDelegate">The event dispatcher delegate.</param>
        public SqlServerEventDispatcher(string connectionString, string schema, Action<long, object> eventDispatcherDelegate)
            : this(connectionString, schema, new CustomEventDispatcher(eventDispatcherDelegate))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerEventDispatcher"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="schema">The schema.</param>
        /// <param name="eventDispatcherDelegate">The event dispatcher delegate.</param>
        /// <param name="dispatcherId">The dispatcher identifier.</param>
        public SqlServerEventDispatcher(string connectionString, string schema, Action<long, object> eventDispatcherDelegate, string dispatcherId)
            : this(connectionString, schema, new CustomEventDispatcher(eventDispatcherDelegate), dispatcherId)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerEventDispatcher"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="eventDispatcher">The event dispatcher.</param>
        public SqlServerEventDispatcher(string connectionString, IEventDispatcher eventDispatcher)
            : this(connectionString, "dbo", Guid.Empty, eventDispatcher)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerEventDispatcher"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="eventDispatcher">The event dispatcher.</param>
        /// <param name="dispatcherId">The dispatcher identifier.</param>
        public SqlServerEventDispatcher(string connectionString, IEventDispatcher eventDispatcher, string dispatcherId)
            : this(connectionString, "dbo", Guid.Empty, eventDispatcher, dispatcherId)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerEventDispatcher" /> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="schema">The schema.</param>
        /// <param name="eventDispatcher">The event dispatcher.</param>
        public SqlServerEventDispatcher(string connectionString, string schema, IEventDispatcher eventDispatcher)
             : this(connectionString, schema, Guid.Empty, eventDispatcher)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerEventDispatcher"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="schema">The schema.</param>
        /// <param name="eventDispatcher">The event dispatcher.</param>
        /// <param name="dispatcherId">The dispatcher identifier.</param>
        public SqlServerEventDispatcher(string connectionString, string schema, IEventDispatcher eventDispatcher, string dispatcherId)
            : this(connectionString, schema, Guid.Empty, eventDispatcher, dispatcherId)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerEventDispatcher"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="partition">The partition.</param>
        /// <param name="eventDispatcherDelegate">The event dispatcher delegate.</param>
        internal SqlServerEventDispatcher(string connectionString, Guid partition, Action<long, object> eventDispatcherDelegate)
            : this(connectionString, partition, new CustomEventDispatcher(eventDispatcherDelegate))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerEventDispatcher"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="partition">The partition.</param>
        /// <param name="eventDispatcherDelegate">The event dispatcher delegate.</param>
        /// <param name="dispatcherId">The dispatcher identifier.</param>
        internal SqlServerEventDispatcher(string connectionString, Guid partition, Action<long, object> eventDispatcherDelegate, string dispatcherId)
            : this(connectionString, partition, new CustomEventDispatcher(eventDispatcherDelegate), dispatcherId)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerEventDispatcher"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="schema">The schema.</param>
        /// <param name="partition">The partition.</param>
        /// <param name="eventDispatcherDelegate">The event dispatcher delegate.</param>
        internal SqlServerEventDispatcher(string connectionString, string schema, Guid partition, Action<long, object> eventDispatcherDelegate)
            : this(connectionString, schema, partition, new CustomEventDispatcher(eventDispatcherDelegate))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerEventDispatcher"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="schema">The schema.</param>
        /// <param name="partition">The partition.</param>
        /// <param name="eventDispatcherDelegate">The event dispatcher delegate.</param>
        /// <param name="dispatcherId">The dispatcher identifier.</param>
        internal SqlServerEventDispatcher(
            string connectionString,
            string schema,
            Guid partition,
            Action<long, object> eventDispatcherDelegate,
            string dispatcherId)
            : this(
                  connectionString,
                  schema,
                  partition,
                  new CustomEventDispatcher(eventDispatcherDelegate),
                  dispatcherId)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerEventDispatcher"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="partition">The partition.</param>
        /// <param name="eventDispatcher">The event dispatcher.</param>
        internal SqlServerEventDispatcher(string connectionString, Guid partition, IEventDispatcher eventDispatcher)
             : this(connectionString, "dbo", partition, eventDispatcher, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerEventDispatcher"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="partition">The partition.</param>
        /// <param name="eventDispatcher">The event dispatcher.</param>
        /// <param name="dispatcherId">The dispatcher identifier.</param>
        internal SqlServerEventDispatcher(string connectionString, Guid partition, IEventDispatcher eventDispatcher, string dispatcherId)
            : this(connectionString, "dbo", partition, eventDispatcher, dispatcherId)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerEventDispatcher"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="schema">The schema.</param>
        /// <param name="partition">The partition.</param>
        /// <param name="eventDispatcher">The event dispatcher.</param>
        internal SqlServerEventDispatcher(string connectionString, string schema, Guid partition, IEventDispatcher eventDispatcher)
             : this(connectionString, schema, partition, eventDispatcher, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerEventDispatcher"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="schema">The schema.</param>
        /// <param name="partition">The partition.</param>
        /// <param name="eventDispatcher">The event dispatcher.</param>
        /// <param name="dispatcherId">The dispatcher identifier.</param>
        internal SqlServerEventDispatcher(
            string connectionString,
            string schema,
            Guid partition,
            IEventDispatcher eventDispatcher,
            string dispatcherId)
            : this(
               eventDispatcher,
               new SqlServerEventStore(connectionString, schema, partition),
               new SqlServerNotificationService(connectionString, schema, partition),
               dispatcherId)
        {
        }

        private SqlServerEventDispatcher(
            IEventDispatcher eventDispatcher,
            IEventStore eventStore,
            SqlServerNotificationService notificationService,
            string dispatcherId)
            : base(eventDispatcher, eventStore, notificationService, dispatcherId, 50)
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

                this.isDisposed = true;
            }

            base.Dispose(disposing);
        }
    }
}
