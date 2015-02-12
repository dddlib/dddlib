// <copyright file="Entity.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib
{
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using dddlib.Runtime;
    using dddlib.Sdk;

    /// <summary>
    /// Represents an entity.
    /// </summary>
    //// TODO (Cameron): Ensure that an entity can be created without a natural key.
    public abstract class Entity
    {
        private readonly NaturalKeySelector naturalKey;
        private readonly IEqualityComparer<object> naturalKeyEqualityComparer;

        internal Entity(EntityType entityType)
        {
            Guard.Against.Null(() => entityType);

            this.naturalKey = entityType.NaturalKeySelector;
            this.naturalKeyEqualityComparer = entityType.NaturalKeyEqualityComparer;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Entity"/> class.
        /// </summary>
        protected Entity()
        {
            var entityType = Application.Current.GetEntityType(this.GetType());

            this.naturalKey = entityType.NaturalKeySelector;
            this.naturalKeyEqualityComparer = entityType.NaturalKeyEqualityComparer;
        }

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
            var value = this.naturalKey.Invoke(this);
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

            if (this.naturalKey == null)
            {
                // NOTE (Cameron): This entity has no natural key defined and doesn't meet the [previous] criteria for .NET reference equality.
                return false;
            }

            var thisValue = this.naturalKey.Invoke(this);
            var otherValue = this.naturalKey.Invoke(other);

            return this.naturalKeyEqualityComparer.Equals(thisValue, otherValue);
        }
    }
}
