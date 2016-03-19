// <copyright file="RuntimeException.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Reflection;
    using System.Runtime.Serialization;

    /// <summary>
    /// Represents a runtime exception.
    /// </summary>
    [Serializable]
    public class RuntimeException : Exception
    {
        private const string OriginalMessage = "OriginalMessage";

        private string originalMessage;

        /// <summary>
        /// Initializes a new instance of the <see cref="RuntimeException"/> class.
        /// </summary>
        public RuntimeException()
            : this((Exception)null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RuntimeException"/> class.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public RuntimeException(string message)
            : this(message, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RuntimeException"/> class.
        /// </summary>
        /// <param name="inner">The inner exception.</param>
        public RuntimeException(Exception inner)
            : this("A runtime exception has occurred.", inner)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RuntimeException"/> class.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="inner">The inner exception.</param>
        public RuntimeException(string message, Exception inner)
            : base(message, inner)
        {
            this.originalMessage = message;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RuntimeException"/> class.
        /// </summary>
        /// <param name="info">The <see cref="T:System.Runtime.Serialization.SerializationInfo" /> that holds the serialized object data about the exception being thrown.</param>
        /// <param name="context">The <see cref="T:System.Runtime.Serialization.StreamingContext" /> that contains contextual information about the source or destination.</param>
        protected RuntimeException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            this.originalMessage = info.GetString(OriginalMessage);
        }

        /// <summary>
        /// Gets or sets a link to the help file associated with this exception.
        /// </summary>
        /// <value>The help link.</value>
        public override string HelpLink
        {
            get
            {
                return base.HelpLink;
            }

            set
            {
                var message = value == null
                    ? this.originalMessage
                    : string.IsNullOrWhiteSpace(this.originalMessage)
                        ? value
                        : string.Concat(this.originalMessage, "\r\nFurther information: ", value);

                typeof(RuntimeException).GetField("_message", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(this, message);

                base.HelpLink = value;
            }
        }

        /// <summary>
        /// When overridden in a derived class, sets the <see cref="T:System.Runtime.Serialization.SerializationInfo" /> with information about the exception.
        /// </summary>
        /// <param name="info">The <see cref="T:System.Runtime.Serialization.SerializationInfo" /> that holds the serialized object data about the exception being thrown.</param>
        /// <param name="context">The <see cref="T:System.Runtime.Serialization.StreamingContext" /> that contains contextual information about the source or destination.</param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            Guard.Against.Null(() => info);

            base.GetObjectData(info, context);

            info.AddValue(OriginalMessage, this.originalMessage);
        }
    }
}
