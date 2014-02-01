// <copyright file="IApplication.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;

    /// <summary>
    /// Exposes the public members of the application.
    /// </summary>
    public interface IApplication
    {
        /// <summary>
        /// Adds the aggregate factory to the application.
        /// </summary>
        /// <typeparam name="T">The type of aggregate.</typeparam>
        /// <param name="aggregateFactory">The aggregate factory.</param>
        void AddFactory<T>(Func<T> aggregateFactory) where T : AggregateRoot;
    }
}
