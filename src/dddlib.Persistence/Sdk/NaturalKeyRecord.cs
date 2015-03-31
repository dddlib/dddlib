// <copyright file="NaturalKeyRecord.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Sdk
{
    using System;

    /// <summary>
    /// Represents a natural key record.
    /// </summary>
    public class NaturalKeyRecord
    {
        /// <summary>
        /// Gets or sets the identity of the natural key.
        /// </summary>
        /// <value>The identity of the natural key.</value>
        public Guid Identity { get; set; }

        /// <summary>
        /// Gets or sets the serialized value of the natural key.
        /// </summary>
        /// <value>The serialized value of the natural key.</value>
        public string SerializedValue { get; set; }

        /// <summary>
        /// Gets or sets the checkpoint for the natural key.
        /// </summary>
        /// <value>The checkpoint.</value>
        public long Checkpoint { get; set; }
    }
}
