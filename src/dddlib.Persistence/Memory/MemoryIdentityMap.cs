using dddlib.Persistence.Sdk;

namespace dddlib.Persistence.Memory;

public sealed class MemoryIdentityMap : DefaultIdentityMap
{
    public MemoryIdentityMap()
        : base(new MemoryNaturalKeyRepository(), new DefaultNaturalKeySerializer())
    {
    }
}
