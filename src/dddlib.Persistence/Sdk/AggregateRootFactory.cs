// <copyright file="AggregateRootFactory.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

#if dddlibTestsSdk
namespace dddlib.Tests.Sdk
#else
namespace dddlib.Persistence.Sdk
#endif
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using dddlib.Runtime;

    /// <summary>
    /// Represents the aggregate root factory.
    /// </summary>
    public sealed class AggregateRootFactory
    {
        /// <summary>
        /// Creates an aggregate root.
        /// </summary>
        /// <typeparam name="T">The type of aggregate root.</typeparam>
        /// <param name="memento">The memento.</param>
        /// <param name="revision">The revision.</param>
        /// <param name="events">The events.</param>
        /// <param name="state">The state.</param>
        /// <returns>The aggregate root.</returns>
        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1118:ParameterMustNotSpanMultipleLines", Justification = "It's fine here.")]
        public T Create<T>(object memento, int revision, IEnumerable<object> events, string state)
            where T : AggregateRoot
        {
            var runtimeType = Application.Current.GetAggregateRootType(typeof(T));
            if (runtimeType.UninitializedFactory == null)
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        @"The aggregate root of type '{0}' does not have a factory method for reconstitution registered with the runtime.
To fix this issue, either:
- use a bootstrapper to configure reconstitution for that aggregate root, or
- add a protected internal default constructor to the aggregate root.",
                        typeof(T)))
                {
                    HelpLink = "https://github.com/dddlib/dddlib/wiki/Aggregate-Root-Reconstitution",
                };
            }

            var uninitializedFactory = (Func<T>)runtimeType.UninitializedFactory;
            var aggregateRoot = uninitializedFactory.Invoke();
            aggregateRoot.Initialize(memento, revision, events, state);
            return aggregateRoot;
        }
    }
}
