// <copyright file="Runtime.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Reflection;

    internal class Runtime
    {
        private readonly Dictionary<Assembly, AssemblyDescriptor> assemblyDescriptors = new Dictionary<Assembly, AssemblyDescriptor>();
        private readonly Dictionary<Type, TypeDescriptor> typeDescriptors = new Dictionary<Type, TypeDescriptor>();

        private readonly bool treatAllRuntimeIssuesAsFatal;

        public Runtime(bool treatAllRuntimeIssuesAsFatal)
        {
            this.treatAllRuntimeIssuesAsFatal = treatAllRuntimeIssuesAsFatal;
        }

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
