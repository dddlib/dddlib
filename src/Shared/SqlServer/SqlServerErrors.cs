using Microsoft.Data.SqlClient;

namespace dddlib.Persistence.SqlServer;

internal static class SqlServerErrors
{
    public const int CommitStateMismatch = 50409;
    public const int LockTimeout = 50500;
    public const int LockRequestTimeout = 1222;
    public const int PrimaryKeyViolation = 2627;
    public const int UniqueIndexViolation = 2601;
    public const int Deadlock = 1205;

    public static bool Has(this SqlException exception, int number) =>
        exception.Errors.Cast<SqlError>().Any(error => error.Number == number);
}
