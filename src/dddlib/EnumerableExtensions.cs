// <copyright file="EnumerableExtensions.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

[module: System.Diagnostics.CodeAnalysis.SuppressMessage(
    "StyleCop.CSharp.NamingRules", 
    "SA1300:ElementMustBeginWithUpperCaseLetter", 
    Justification = "Noted.")]

namespace dddlib
{
    using System;
    using System.Collections.Generic;

    internal static class EnumerableExtensions
    {
        // LINK (Cameron): http://social.msdn.microsoft.com/forums/en-US/linqprojectgeneral/thread/fe3d441d-1e49-4855-8ae8-60068b3ef741/
        public static IEnumerable<TSource> Traverse<TSource>(this IEnumerable<TSource> source, Func<TSource, IEnumerable<TSource>> recursiveFunction)
        {
            foreach (var item in source)
            {
                yield return item;

                var recursiveResult = recursiveFunction(item);
                if (recursiveResult == null)
                {
                    continue;
                }

                foreach (var itemRecurse in Traverse(recursiveResult, recursiveFunction))
                {
                    yield return itemRecurse;
                }
            }
        }
    }
}