// <copyright file="TypeParameterException.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// Represents a type parameter exception.
    /// </summary>
    [Serializable]
    public class TypeParameterException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TypeParameterException"/> class.
        /// </summary>
        public TypeParameterException()
            : this((string)null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TypeParameterException"/> class.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public TypeParameterException(string message)
            : this(message, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TypeParameterException"/> class.
        /// </summary>
        /// <param name="inner">The inner.</param>
        public TypeParameterException(Exception inner)
            : this(null, inner)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TypeParameterException"/> class.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="inner">The inner.</param>
        public TypeParameterException(string message, Exception inner)
            : base(message, inner)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TypeParameterException"/> class.
        /// </summary>
        /// <param name="info">The <see cref="T:System.Runtime.Serialization.SerializationInfo" /> that holds the serialized object data about the exception being thrown.</param>
        /// <param name="context">The <see cref="T:System.Runtime.Serialization.StreamingContext" /> that contains contextual information about the source or destination.</param>
        protected TypeParameterException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
