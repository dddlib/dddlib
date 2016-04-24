// <copyright file="MemoryIdentityMap.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Memory
{
    using System;
    using dddlib.Persistence.Sdk;

    /// <summary>
    /// Represents the memory-based identity map.
    /// </summary>
    public sealed class MemoryIdentityMap : DefaultIdentityMap, IDisposable
    {
        private readonly MemoryNaturalKeyRepository repository;

        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryIdentityMap" /> class.
        /// </summary>
        public MemoryIdentityMap()
            : this(new MemoryNaturalKeyRepository(), new DefaultNaturalKeySerializer())
        {
        }

        private MemoryIdentityMap(MemoryNaturalKeyRepository repository, INaturalKeySerializer serializer)
            : base(repository, serializer)
        {
            this.repository = repository;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }

            this.repository.Dispose();

            this.isDisposed = true;
        }
    }
}
