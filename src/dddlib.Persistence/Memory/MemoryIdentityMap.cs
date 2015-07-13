// <copyright file="MemoryIdentityMap.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Memory
{
    using dddlib.Persistence.Sdk;

    /// <summary>
    /// Represents a memory-based identity map.
    /// </summary>
    public class MemoryIdentityMap : DefaultIdentityMap
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryIdentityMap" /> class.
        /// </summary>
        public MemoryIdentityMap()
            : base(new MemoryNaturalKeyRepository(), new DefaultNaturalKeySerializer())
        {
        }
    }
}
