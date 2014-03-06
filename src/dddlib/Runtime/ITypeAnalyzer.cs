// <copyright file="ITypeAnalyzer.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using dddlib.Configuration;

    internal interface ITypeAnalyzer
    {
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1632:DocumentationTextMustMeetMinimumCharacterLength", Justification = "Not here.")]
        TypeDescriptor GetDescriptor(Type type, TypeConfiguration configuration);
    }
}
