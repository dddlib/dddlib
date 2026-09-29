namespace dddlib.Persistence.Sdk;

public sealed record NaturalKeyRecord(Guid Identity, string SerializedValue, long Checkpoint, bool IsRemoved);
