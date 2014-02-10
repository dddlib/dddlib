// <copyright file="Domain.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Reflection;
    using dddlib.Runtime.Analyzer;

    /// <summary>
    /// Represents a domain.
    /// </summary>
    public class Domain
    {
        private readonly Dictionary<Assembly, AssemblyDescriptor> assemblyDescriptors = new Dictionary<Assembly, AssemblyDescriptor>();
        private readonly Dictionary<Type, TypeDescriptor> typeDescriptors = new Dictionary<Type, TypeDescriptor>();

        /// <summary>
        /// Gets the <see cref="AssemblyDescriptor"/> for the specified assembly.
        /// </summary>
        /// <value>The <see cref="AssemblyDescriptor"/>.</value>
        /// <param name="assembly">The assembly.</param>
        /// <returns>The assembly descriptor.</returns>
        public AssemblyDescriptor this[Assembly assembly]
        {
            get 
            {
                var assemblyDescriptor = default(AssemblyDescriptor);
                if (this.assemblyDescriptors.TryGetValue(assembly, out assemblyDescriptor))
                {
                    return assemblyDescriptor;
                }

                lock (this.assemblyDescriptors)
                {
                    if (this.assemblyDescriptors.TryGetValue(assembly, out assemblyDescriptor))
                    {
                        return assemblyDescriptor;
                    }

                    this.assemblyDescriptors.Add(assembly, assemblyDescriptor = new AssemblyAnalyzer().GetDescriptor(assembly));

                    return assemblyDescriptor;
                }
            }
        }

        /// <summary>
        /// Gets the <see cref="TypeDescriptor"/> for the specified type.
        /// </summary>
        /// <value>The <see cref="TypeDescriptor"/>.</value>
        /// <param name="type">The type.</param>
        /// <returns>The type descriptor.</returns>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1632:DocumentationTextMustMeetMinimumCharacterLength", Justification = "Not here.")]
        public TypeDescriptor this[Type type]
        {
            get 
            {
                var typeDescriptor = default(TypeDescriptor);
                if (this.typeDescriptors.TryGetValue(type, out typeDescriptor))
                {
                    return typeDescriptor;
                }

                lock (this.typeDescriptors)
                {
                    if (this.typeDescriptors.TryGetValue(type, out typeDescriptor))
                    {
                        return typeDescriptor;
                    }

                    this.typeDescriptors.Add(type, typeDescriptor = new TypeAnalyzer(this[type.Assembly]).GetDescriptor(type));

                    return typeDescriptor;
                }
            }
        }
    }
}
