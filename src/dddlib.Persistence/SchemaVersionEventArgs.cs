namespace dddlib.Persistence;

public sealed class SchemaVersionEventArgs(SchemaVersion version) : EventArgs
{
    public SchemaVersion Version { get; } = version;
}
