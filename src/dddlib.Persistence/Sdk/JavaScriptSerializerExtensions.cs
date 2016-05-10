// <copyright file="JavaScriptSerializerExtensions.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

#if PERSISTENCE
namespace dddlib.Sdk
#elif DISPATCHER
namespace dddlib.Persistence.EventDispatcher.Sdk
#elif PROJECTIONS
namespace dddlib.Projections.Sdk
#endif
{
    using System;
    using System.Collections.Concurrent;
    using System.Web.Script.Serialization;

    /// <summary>
    /// Contains extension methods for <see cref="System.Web.Script.Serialization.JavaScriptSerializer"/>.
    /// </summary>
    public static class JavaScriptSerializerExtensions
    {
        private static readonly ConcurrentDictionary<string, Type> ResolvedTypes = new ConcurrentDictionary<string, Type>();

        /// <summary>
        /// Gets the System.Type with the specified name, performing a case-sensitive search.
        /// </summary>
        /// <param name="serializer">The serializer.</param>
        /// <param name="typeName">Name of the type.</param>
        /// <returns>The type with the specified name, if found; otherwise, null.</returns>
        public static Type GetType(this JavaScriptSerializer serializer, string typeName)
        {
            return ResolvedTypes.GetOrAdd(typeName, key => Type.GetType(key));
        }
    }
}
