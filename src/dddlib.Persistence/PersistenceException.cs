// <copyright file="PersistenceException.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// Represents a persistence exception.
    /// </summary>
    [Serializable]
    public class PersistenceException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PersistenceException"/> class.
        /// </summary>
        public PersistenceException()
            : this((string)null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PersistenceException"/> class.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public PersistenceException(string message)
            : this(message, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PersistenceException"/> class.
        /// </summary>
        /// <param name="inner">The inner exception.</param>
        public PersistenceException(Exception inner)
            : this("A persistence exception has occurred.", inner)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PersistenceException"/> class.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="inner">The inner exception.</param>
        public PersistenceException(string message, Exception inner)
            : base(message, inner)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PersistenceException"/> class.
        /// </summary>
        /// <param name="info">The <see cref="T:System.Runtime.Serialization.SerializationInfo" /> that holds the serialized object data about the exception being thrown.</param>
        /// <param name="context">The <see cref="T:System.Runtime.Serialization.StreamingContext" /> that contains contextual information about the source or destination.</param>
        protected PersistenceException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
