// <copyright file="SqlServerGuardExtensions.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.SqlServer
{
    using System;
    using System.Data.SqlClient;

    internal static class SqlServerGuardExtensions
    {
        public static void InvalidConnectionString(this Guard guard, Func<string> expression)
        {
            using (var connection = new SqlConnection(expression()))
            {
                try
                {
                    connection.Open();
                }
                catch (SqlException ex)
                {
                    throw new ArgumentException(
                        "Value must be a valid connection string. See inner exception for details.",
                        Guard.Expression.Parse(expression),
                        ex);
                }
            }
        }

        public static void NegativeOrZero(this Guard guard, Func<int> expression)
        {
            var value = expression();

            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(Guard.Expression.Parse(expression), value, "Value has to be positive.");
            }
        }
    }
}
