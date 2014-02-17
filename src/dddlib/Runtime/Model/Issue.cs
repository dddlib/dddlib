// <copyright file="Issue.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime.Model
{
    using System.Collections.Generic;

    /// <summary>
    /// Represents an issue.
    /// </summary>
    public class Issue : dddlib.ValueObject<Issue>
    {
        /// <summary>
        /// Gets each of the items that together comprise the value of this object.
        /// </summary>
        /// <returns>An enumeration of the items that together comprise the value of this object.</returns>
        protected override IEnumerable<object> GetValue()
        {
            yield break;
        }
    }
}
