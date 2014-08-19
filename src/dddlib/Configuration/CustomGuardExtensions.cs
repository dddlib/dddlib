// <copyright file="CustomGuardExtensions.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Configuration
{
    using System;
    using System.Linq.Expressions;

    internal static class CustomGuardExtensions
    {
        public static void InvalidExpression<T, TKey>(
            this Guard guard, Func<Expression<Func<T, TKey>>> memberExpression, out MemberExpression expression)
        {
            Guard.Against.Null(memberExpression);

            expression = memberExpression().Body as MemberExpression;
            if (expression == null)
            {
                throw new Exception("not a memberexpression");
            }
        }
    }
}
