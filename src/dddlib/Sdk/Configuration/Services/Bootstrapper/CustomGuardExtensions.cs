// <copyright file="CustomGuardExtensions.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk.Configuration.Services.Bootstrapper
{
    using System;
    using System.Linq.Expressions;

    internal static class CustomGuardExtensions
    {
        public static void InvalidMemberExpression<T>(this Guard guard, Func<Expression<T>> expression, out MemberExpression memberExpression)
        {
            Guard.Against.Null(expression);

            memberExpression = expression().Body as MemberExpression;
            if (memberExpression == null)
            {
                throw new ArgumentException("Value must be a member expression.", Guard.Expression.Parse(expression));
            }
        }
    }
}
