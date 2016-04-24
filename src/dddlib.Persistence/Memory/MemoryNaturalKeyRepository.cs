// <copyright file="MemoryNaturalKeyRepository.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Memory
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO.MemoryMappedFiles;
    using System.Linq;
    using System.Runtime.Serialization;
    using System.Security.AccessControl;
    using System.Security.Principal;
    using System.Text;
    using System.Threading;
    using System.Web.Script.Serialization;
    using dddlib.Persistence.Sdk;
    using dddlib.Sdk;

    /// <summary>
    /// Represents the memory natural key repository.
    /// </summary>
    public sealed class MemoryNaturalKeyRepository : INaturalKeyRepository, IDisposable
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        private readonly Dictionary<Type, List<MemoryMappedNaturalKey>> naturalKeysTypes = new Dictionary<Type, List<MemoryMappedNaturalKey>>();
        private readonly List<MemoryMappedNaturalKey> store = new List<MemoryMappedNaturalKey>();

        private readonly Mutex mutex;
        private readonly MemoryMappedFile file;

        private long currentCheckpoint;
        private long readOffset;
        private long writeOffset;
        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryNaturalKeyRepository"/> class.
        /// </summary>
        public MemoryNaturalKeyRepository()
        {
            var securitySettings = new MutexSecurity();
            securitySettings.AddAccessRule(
                new MutexAccessRule(
                new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                MutexRights.FullControl,
                AccessControlType.Allow));

            var mutexCreated = false;
            this.mutex = new Mutex(false, @"Global\MemoryNaturalKeyRepository3Mutex", out mutexCreated, securitySettings);
            this.file = MemoryMappedFile.CreateOrOpen("MemoryNaturalKeyRepository3", 1 * 1024 * 1024 /* 1MB */);

            Serializer.RegisterConverters(new[] { new DateTimeConverter() });
        }

        /// <summary>
        /// Gets the natural key records for the specified type of aggregate root from the specified checkpoint.
        /// </summary>
        /// <param name="aggregateRootType">Type of the aggregate root.</param>
        /// <param name="checkpoint">The checkpoint.</param>
        /// <returns>The natural key records.</returns>
        public IEnumerable<NaturalKeyRecord> GetNaturalKeys(Type aggregateRootType, long checkpoint)
        {
            Guard.Against.Null(() => aggregateRootType);

            if (this.isDisposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }

            this.Synchronize();

            var naturalKeysType = default(List<MemoryMappedNaturalKey>);
            if (!this.naturalKeysTypes.TryGetValue(aggregateRootType, out naturalKeysType))
            {
                return new NaturalKeyRecord[0];
            }

            return naturalKeysType
                .Where(naturalKey => naturalKey.Checkpoint >= checkpoint)
                .Select(naturalKey =>
                    new NaturalKeyRecord
                    {
                        Identity = naturalKey.Identity,
                        SerializedValue = naturalKey.SerializedValue,
                        Checkpoint = naturalKey.Checkpoint,
                        IsRemoved = naturalKey.IsRemoved,
                    })
                .ToList();
        }

        /// <summary>
        /// Attempts to add the natural key to the natural key records.
        /// </summary>
        /// <param name="aggregateRootType">Type of the aggregate root.</param>
        /// <param name="serializedNaturalKey">The serialized natural key.</param>
        /// <param name="checkpoint">The checkpoint.</param>
        /// <param name="naturalKeyRecord">The natural key record.</param>
        /// <returns>Returns <c>true</c> if the natural key record was successfully added; otherwise <c>false</c>.</returns>
        public bool TryAddNaturalKey(Type aggregateRootType, object serializedNaturalKey, long checkpoint, out NaturalKeyRecord naturalKeyRecord)
        {
            Guard.Against.Null(() => aggregateRootType);
            Guard.Against.Null(() => serializedNaturalKey);

            if (this.isDisposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }

            using (new ExclusiveCodeBlock(this.mutex))
            {
                this.Synchronize();

                var naturalKeysType = default(List<MemoryMappedNaturalKey>);
                var maxNaturalKeysTypeCheckpoint = this.naturalKeysTypes.TryGetValue(aggregateRootType, out naturalKeysType)
                    ? naturalKeysType.Max(naturalKey => naturalKey.Checkpoint)
                    : 0L;

                if (maxNaturalKeysTypeCheckpoint != checkpoint)
                {
                    naturalKeyRecord = null;
                    return false;
                }

                var memoryMappedNaturalKey = new MemoryMappedNaturalKey
                {
                    Identity = Guid.NewGuid(),
                    Type = aggregateRootType.GetSerializedName(),
                    SerializedValue = (string)serializedNaturalKey,
                    Checkpoint = checkpoint + 1,
                    IsRemoved = false,
                };

                var buffer = Encoding.UTF8.GetBytes(Serializer.Serialize(memoryMappedNaturalKey));

                using (var accessor = this.file.CreateViewAccessor(this.writeOffset, 2 + buffer.Length))
                {
                    accessor.Write(0, (ushort)buffer.Length);
                    accessor.WriteArray(2, buffer, 0, buffer.Length);
                }

                this.writeOffset += 2 + buffer.Length;

                naturalKeyRecord = new NaturalKeyRecord
                {
                    Identity = memoryMappedNaturalKey.Identity,
                    SerializedValue = memoryMappedNaturalKey.SerializedValue,
                    Checkpoint = memoryMappedNaturalKey.Checkpoint,
                    IsRemoved = memoryMappedNaturalKey.IsRemoved,
                };
            }

            return true;
        }

        /// <summary>
        /// Removes the natural key with the specified identity from the natural key records.
        /// </summary>
        /// <param name="naturalKeyIdentity">The natural key identity.</param>
        public void Remove(Guid naturalKeyIdentity)
        {
            if (this.isDisposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }

            using (new ExclusiveCodeBlock(this.mutex))
            {
                this.Synchronize();

                var naturalKeyRecords = this.store.Where(naturalKey => naturalKey.Identity == naturalKeyIdentity);
                if (naturalKeyRecords.Any(naturalKey => naturalKey.IsRemoved) || naturalKeyRecords.SingleOrDefault() == null)
                {
                    return;
                }

                var memoryMappedNaturalKey = new MemoryMappedNaturalKey
                {
                    Identity = naturalKeyIdentity,
                    Type = naturalKeyRecords.Single().Type,
                    SerializedValue = naturalKeyRecords.Single().SerializedValue,
                    Checkpoint = this.currentCheckpoint + 1,
                    IsRemoved = true,
                };

                var buffer = Encoding.UTF8.GetBytes(Serializer.Serialize(memoryMappedNaturalKey));

                using (var accessor = this.file.CreateViewAccessor(this.writeOffset, 2 + buffer.Length))
                {
                    accessor.Write(0, (ushort)buffer.Length);
                    accessor.WriteArray(2, buffer, 0, buffer.Length);
                }

                this.writeOffset += 2 + buffer.Length;
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1118:ParameterMustNotSpanMultipleLines", Justification = "It's fine here.")]
        public void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }

            this.mutex.Dispose();
            this.file.Dispose();

            this.isDisposed = true;
        }

        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1118:ParameterMustNotSpanMultipleLines", Justification = "It's fine here.")]
        private void Synchronize()
        {
            var length = 0;
            do
            {
                using (var accessor = this.file.CreateViewAccessor(this.readOffset, 2))
                {
                    length = accessor.ReadUInt16(0);
                    if (length == 0)
                    {
                        break;
                    }
                }

                var buffer = new byte[length];
                using (var accessor = this.file.CreateViewAccessor(this.readOffset + 2, length))
                {
                    accessor.ReadArray(0, buffer, 0, length);
                }

                var serializedNaturalKey = Encoding.UTF8.GetString(buffer);
                var memoryMappedNaturalKey = Serializer.Deserialize<MemoryMappedNaturalKey>(serializedNaturalKey);

                this.readOffset += 2 + buffer.Length;

                var naturalKeysType = default(List<MemoryMappedNaturalKey>);
                var naturalKeyType = Type.GetType(memoryMappedNaturalKey.Type);
                if (naturalKeyType == null)
                {
                    throw new SerializationException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            @"Cannot deserialize event into type of '{0}' as that type does not exist in the assembly '{1}' or the assembly is not referenced by the project.
To fix this issue:
- ensure that the assembly '{1}' contains the type '{0}', and
- check that the the assembly '{1}' is referenced by the project.
Further information: https://github.com/dddlib/dddlib/wiki/Serialization",
                            memoryMappedNaturalKey.Type.Split(',').FirstOrDefault(),
                            memoryMappedNaturalKey.Type.Split(',').LastOrDefault()));
                }

                if (!this.naturalKeysTypes.TryGetValue(naturalKeyType, out naturalKeysType))
                {
                    naturalKeysType = new List<MemoryMappedNaturalKey>();
                    this.naturalKeysTypes.Add(naturalKeyType, naturalKeysType);
                }

                naturalKeysType.Add(memoryMappedNaturalKey);
                this.store.Add(memoryMappedNaturalKey);

                this.currentCheckpoint = memoryMappedNaturalKey.Checkpoint;
            }
            while (length > 0);

            this.writeOffset = this.readOffset;
        }

        private class MemoryMappedNaturalKey
        {
            public Guid Identity { get; set; }

            public string Type { get; set; }

            public string SerializedValue { get; set; }

            public long Checkpoint { get; set; }

            public bool IsRemoved { get; set; }
        }
    }
}
