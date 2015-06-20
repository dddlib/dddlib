// <copyright file="AggregateRootNotFoundException.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// Represents an aggregate root not found exception.
    /// </summary>
    [Serializable]
    public class AggregateRootNotFoundException : PersistenceException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AggregateRootNotFoundException"/> class.
        /// </summary>
        public AggregateRootNotFoundException()
            : this((string)null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AggregateRootNotFoundException"/> class.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public AggregateRootNotFoundException(string message)
            : this(message, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AggregateRootNotFoundException"/> class.
        /// </summary>
        /// <param name="inner">The inner exception.</param>
        public AggregateRootNotFoundException(Exception inner)
            : this(null, inner)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AggregateRootNotFoundException"/> class.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="inner">The inner exception.</param>
        public AggregateRootNotFoundException(string message, Exception inner)
            : base(message, inner)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AggregateRootNotFoundException"/> class.
        /// </summary>
        /// <param name="info">The <see cref="T:System.Runtime.Serialization.SerializationInfo" /> that holds the serialized object data about the exception being thrown.</param>
        /// <param name="context">The <see cref="T:System.Runtime.Serialization.StreamingContext" /> that contains contextual information about the source or destination.</param>
        protected AggregateRootNotFoundException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
