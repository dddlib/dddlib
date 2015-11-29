// <copyright file="AssertionExtensions.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Sdk
{
    using System.Web.Script.Serialization;

    public static class AssertionExtensions
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        public static void ShouldMatch(this object actualValue, object expectedValue)
        {
            var actual = Serializer.Serialize(actualValue);
            var expected = Serializer.Serialize(expectedValue);

            if (actual != expected)
            {
                throw new FluentAssertions.Execution.AssertionFailedException("Mementos don't match.");
            }
        }
    }
}
