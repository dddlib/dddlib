// <copyright file="TypeExtensions.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Contains extension methods for <see cref="System.Type"/>.
    /// </summary>
    public static class TypeExtensions
    {
        /// <summary>
        /// Enumerates the type hierarchy until the specified type is reached.
        /// </summary>
        /// <param name="sourceType">The source type.</param>
        /// <param name="type">The type to enumerate until.</param>
        /// <returns>The type hierarchy until the specified type.</returns>
        public static IEnumerable<Type> GetTypeHierarchyUntil(this Type sourceType, Type type)
        {
            Guard.Against.Null(() => sourceType);
            Guard.Against.Null(() => type);

            do
            {
                yield return sourceType;
            }
            while ((sourceType = sourceType.BaseType) != type && sourceType != null);
        }
    }
}