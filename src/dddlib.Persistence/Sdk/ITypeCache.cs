// <copyright file="ITypeCache.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Sdk
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Exposes the public members of the type cache.
    /// </summary>
    public interface ITypeCache
    {
        /// <summary>
        /// Gets the type identifier for the specified type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>The type identifier.</returns>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1632:DocumentationTextMustMeetMinimumCharacterLength", Justification = "Reviewed.")]
        int GetTypeId(Type type);

        /// <summary>
        /// Gets the type name.
        /// </summary>
        /// <param name="typeId">The type identifier.</param>
        /// <returns>The type name.</returns>
        string GetTypeName(int typeId);

        /// <summary>
        /// Gets the type for the specified type identifier.
        /// </summary>
        /// <param name="typeId">The type identifier.</param>
        /// <returns>The type.</returns>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1632:DocumentationTextMustMeetMinimumCharacterLength", Justification = "Reviewed.")]
        Type GetType(int typeId);
    }
}
