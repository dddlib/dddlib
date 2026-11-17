using Microsoft.Data.SqlClient;

namespace dddlib.Persistence.SqlServer;

/// <summary>
/// A SQL Server memento repository as a <see cref="SqlServerBatch"/> and <see cref="SqlServerMementoWriter"/> see it,
/// whatever its aggregate root type.
/// </summary>
internal interface ISqlServerBatchParticipant
{
    /// <summary>
    /// Gets the connection string of the repository, so that a batch can check that it writes the same database.
    /// </summary>
    string ConnectionString { get; }

    /// <summary>
    /// Gets the quoted dddlib schema the repository writes.
    /// </summary>
    string Schema { get; }

    /// <summary>
    /// Gets a value indicating whether the repository stores its mementos in the dddlib <c>Mementos</c> table rather
    /// than through <see cref="SaveMementoAsync"/>.
    /// </summary>
    bool UsesMementoTable { get; }

    /// <summary>
    /// Gets the metadata stored with the events of the repository's aggregate roots beside dddlib's own.
    /// </summary>
    IReadOnlyDictionary<string, string>? EventMetadata { get; }

    /// <summary>
    /// Writes one memento in the given transaction and returns its new state token.
    /// </summary>
    Task<string> SaveMementoAsync(SqlTransaction transaction, Guid id, object memento, string? preCommitState, CancellationToken cancellationToken);
}
