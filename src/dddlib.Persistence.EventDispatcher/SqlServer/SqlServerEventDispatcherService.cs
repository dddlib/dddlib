// <copyright file="SqlServerEventDispatcherService.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.EventDispatcher.SqlServer
{
    using dddlib.Persistence.EventDispatcher.Sdk;

    /// <summary>
    /// Represents the SQL Server event dispatcher service.
    /// </summary>
    //// TODO (Cameron): Manage disposable dependencies.
    public class SqlServerEventDispatcherService : EventDispatcherService
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerEventDispatcherService"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="eventDispatcher">The event dispatcher.</param>
        public SqlServerEventDispatcherService(string connectionString, IEventDispatcher eventDispatcher)
             : base(
                new SqlServerEventStore(connectionString),
                eventDispatcher,
                new SqlServerNotificationService(connectionString),
                50)
        {
        }
    }
}
