// <copyright file="Entity.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using dddlib.Runtime;
    using dddlib.Sdk.Configuration.Model;

    /// <summary>
    /// Represents an entity.
    /// </summary>
    //// TODO (Cameron): Ensure that an entity can be created without a natural key.
    public abstract partial class Entity
    {
        // PERF (Cameron): Introduced to reduce allocations of the function delegates for type information.
        private static readonly Func<Entity, TypeInformation> GetTypeInformation =
            @this => new TypeInformation(Application.Current.GetEntityType(@this.GetType()));

        private readonly TypeInformation typeInformation;

        private bool isDestroyed;

        /// <summary>
        /// Initializes a new instance of the <see cref="Entity"/> class.
        /// </summary>
        protected Entity()
            : this(GetTypeInformation)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Entity"/> class.
        /// </summary>
        /// <param name="entityType">The entity type.</param>
        protected Entity(EntityType entityType)
            : this(@this => new TypeInformation(entityType))
        {
        }

        // LINK (Cameron): http://stackoverflow.com/questions/2287636/pass-current-object-type-into-base-constructor-call
        private Entity(Func<Entity, TypeInformation> getTypeInformation)
        {
            this.typeInformation = getTypeInformation(this);
        }

        /// <summary>
        /// Gets a value indicating whether this instance has been destroyed.
        /// </summary>
        /// <value>Returns <c>true</c> if the lifecycle of this instance has ended; otherwise, <c>false</c>.</value>
        protected internal bool IsDestroyed
        {
            get { return this.isDestroyed; }
        }

#pragma warning disable 1591
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1600:ElementsMustBeDocumented", Justification = "Not visible anywhere.")]
        public static bool operator ==(Entity first, Entity second)
        {
            return object.Equals(first, second);
        }

        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1600:ElementsMustBeDocumented", Justification = "Not visible anywhere.")]
        public static bool operator !=(Entity first, Entity second)
        {
            return !(first == second);
        }
#pragma warning restore 1591

        /// <summary>
        /// Determines whether the specified <see cref="System.Object" /> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="System.Object" /> to compare with this instance.</param>
        /// <returns>Returns <c>true</c> if the specified <see cref="System.Object" /> is equal to this instance; otherwise, <c>false</c>.</returns>
        public sealed override bool Equals(object obj)
        {
            return this.Equals(obj as Entity);
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table.</returns>
        public sealed override int GetHashCode()
        {
            var value = this.typeInformation.GetNaturalKeyValue(this);
            return value == null ? 0 : value.GetHashCode();
        }

        /// <summary>
        /// Determines whether the specified object is equal to this instance.
        /// </summary>
        /// <param name="other">The object to compare with this instance.</param>
        /// <returns>Returns <c>true</c> if the specified object is equal to this instance; otherwise, <c>false</c>.</returns>
        //// LINK (Cameron): http://www.infoq.com/articles/Equality-Overloading-DotNET
        public bool Equals(Entity other)
        {
            if (object.ReferenceEquals(other, null))
            {
                return false;
            }

            if (object.ReferenceEquals(other, this))
            {
                return true;
            }

            if (other.GetType() != this.GetType())
            {
                // NOTE (Cameron): Type mismatch.
                return false;
            }

            if (!this.typeInformation.HasNaturalKey)
            {
                // NOTE (Cameron): This entity has no natural key defined and doesn't meet the [previous] criteria for .NET reference equality.
                return false;
            }

            var thisValue = this.typeInformation.GetNaturalKeyValue(this);
            var otherValue = this.typeInformation.GetNaturalKeyValue(other);

            return object.Equals(thisValue, otherValue);
        }

        /// <summary>
        /// Throws a <see cref="dddlib.BusinessException"/> if the lifecycle of the entity has ended.
        /// </summary>
        protected void ThrowIfLifecycleEnded()
        {
            this.ThrowIfLifecycleEnded(null);
        }

        /// <summary>
        /// Throws a <see cref="dddlib.BusinessException" /> if the lifecycle of the entity has ended.
        /// </summary>
        /// <param name="eventName">The name of the event that resulted in the exception.</param>
        protected void ThrowIfLifecycleEnded(string eventName)
        {
            if (!this.IsDestroyed)
            {
                return;
            }

            var naturalKeyValue = this.typeInformation.GetNaturalKeyValue == null
                ? null
                : this.typeInformation.GetNaturalKeyValue.Invoke(this);

            var format = string.IsNullOrEmpty(eventName)
                ? naturalKeyValue == null
                    ? "Cannot apply changes because the '{0}' longer exists in the system."
                    : "Cannot apply changes to '{1}' because that '{0}' no longer exists in  the system."
                : naturalKeyValue == null
                    ? "Cannot apply '{2}'  because the '{0}' no longer exists in  the system."
                    : "Cannot apply '{2}' to '{1}' because that '{0}' no longer exists in  the system.";

            throw new BusinessException(string.Format(CultureInfo.InvariantCulture, format, this.GetType().Name, naturalKeyValue, eventName));
        }

        /// <summary>
        /// Ends the lifecycle of this instance.
        /// </summary>
        protected void EndLifecycle()
        {
            this.ThrowIfLifecycleEnded();
            this.isDestroyed = true;
        }
    }
}