// <copyright file="NaturalKeyAttribute.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

[module: System.Diagnostics.CodeAnalysis.SuppressMessage(
    "StyleCop.CSharp.NamingRules",
    "SA1300:ElementMustBeginWithUpperCaseLetter",
    Justification = "Noted.")]

namespace dddlib
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Represents the attribute used to identify a natural key on an aggregate root.
    /// </summary>
    [SuppressMessage("Microsoft.Performance", "CA1813:AvoidUnsealedAttributes", Justification = "I will not.")]
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public class NaturalKeyAttribute : Attribute
    {
    }
}
