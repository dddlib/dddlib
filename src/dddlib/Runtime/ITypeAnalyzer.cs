// <copyright file="ITypeAnalyzer.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using dddlib.Runtime.Configuration;

    /// <summary>
    /// Exposes the public members of the type analyzer.
    /// </summary>
    internal interface ITypeAnalyzer
    {
        /// <summary>
        /// Gets the type descriptor for the specified type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="configuration">The type configuration.</param>
        /// <returns>The type descriptor.</returns>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1632:DocumentationTextMustMeetMinimumCharacterLength", Justification = "Not here.")]
        TypeDescriptor GetDescriptor(Type type, TypeConfiguration configuration);
    }
}
