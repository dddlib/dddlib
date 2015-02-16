// <copyright file="Entity.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using dddlib.Runtime;
    using dddlib.Sdk.Configuration.Model;

    /// <summary>
    /// Represents an entity.
    /// </summary>
    //// TODO (Cameron): Ensure that an entity can be created without a natural key.
    public abstract class Entity
    {
        private readonly dddlib.Sdk.Configuration.Model.NaturalKey naturalKey;
        private readonly IEqualityComparer<object> naturalKeyEqualityComparer;

        internal Entity(dddlib.Sdk.Configuration.Model.NaturalKey naturalKey, IEqualityComparer<object> naturalKeyEqualityComparer)
            : this(@this => new Config { NaturalKey = naturalKey, NaturalKeyEqualityComparer = naturalKeyEqualityComparer })
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Entity"/> class.
        /// </summary>
        protected Entity()
            : this(@this => Config.From(Application.Current.GetEntityType(@this.GetType())))
        {
        }

        // LINK (Cameron): http://stackoverflow.com/questions/2287636/pass-current-object-type-into-base-constructor-call
        private Entity(Func<Entity, Config> configureEntity)
        {
            var configuration = configureEntity(this);

            this.naturalKey = configuration.NaturalKey;
            this.naturalKeyEqualityComparer = configuration.NaturalKeyEqualityComparer;
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
            var value = this.naturalKey.GetValue(this);
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

            // TODO (Cameron): Remove NaturalKey.Undefined in preference of null (see above).
            if (this.naturalKey == dddlib.Sdk.Configuration.Model.NaturalKey.Undefined &&
                other.naturalKey == dddlib.Sdk.Configuration.Model.NaturalKey.Undefined)
            {
                return false;
            }

            var thisValue = this.naturalKey.GetValue(this);
            var otherValue = this.naturalKey.GetValue(other);

            return this.naturalKeyEqualityComparer.Equals(thisValue, otherValue);
        }

        private static EntityType GetEntityType(Entity @this)
        {
            return Application.Current.GetEntityType(@this.GetType());
        }

        private class Config
        {
            public dddlib.Sdk.Configuration.Model.NaturalKey NaturalKey { get; set; }

            public IEqualityComparer<object> NaturalKeyEqualityComparer { get; set; }

            public static Config From(EntityType entityType)
            {
                return new Config { NaturalKey = entityType.NaturalKey, NaturalKeyEqualityComparer = entityType.NaturalKeyEqualityComparer };
            }
        }
    }
}
