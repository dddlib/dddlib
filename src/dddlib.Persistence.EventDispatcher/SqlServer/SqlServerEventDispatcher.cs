// <copyright file="SqlServerEventDispatcher.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.EventDispatcher.SqlServer
{
    using dddlib.Persistence.EventDispatcher.Sdk;

    /// <summary>
    /// Represents the SQL Server event dispatcher service.
    /// </summary>
    //// TODO (Cameron): Manage disposable dependencies.
    public class SqlServerEventDispatcher : EventDispatcherService
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerEventDispatcher"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="eventDispatcher">The event dispatcher.</param>
        public SqlServerEventDispatcher(string connectionString, IEventDispatcher eventDispatcher)
             : base(
                new SqlServerEventStore(connectionString),
                eventDispatcher,
                new SqlServerNotificationService(connectionString),
                50)
        {
        }
    }
}
