// <copyright file="Entity.Configuration.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib
{
    using System;
    using System.Collections.Generic;
    using dddlib.Sdk.Configuration.Model;

    /// <content>
    /// Represents an entity.
    /// </content>
    public abstract partial class Entity
    {
        internal Entity(dddlib.Sdk.Configuration.Model.NaturalKey naturalKey, IEqualityComparer<object> naturalKeyEqualityComparer)
            : this(@this => new TypeInformation(naturalKey, naturalKeyEqualityComparer))
        {
        }

        private class TypeInformation
        {
            public TypeInformation(EntityType entityType)
            {
                Guard.Against.Null(() => entityType);

                this.GetNaturalKeyValue = entityType.NaturalKey == null ? (Func<Entity, object>)null : entity => entityType.NaturalKey.GetValue(entity);
                this.NaturalKeyEqualityComparer = entityType.NaturalKeyEqualityComparer;
            }

            public TypeInformation(dddlib.Sdk.Configuration.Model.NaturalKey naturalKey, IEqualityComparer<object> naturalKeyEqualityComparer)
            {
                this.GetNaturalKeyValue = entity => naturalKey.GetValue(entity);
                this.NaturalKeyEqualityComparer = naturalKeyEqualityComparer;
            }

            public bool HasNaturalKey
            {
                get { return this.GetNaturalKeyValue != null; }
            }

            public Func<Entity, object> GetNaturalKeyValue { get; set; }

            public IEqualityComparer<object> NaturalKeyEqualityComparer { get; set; }
        }
    }
}
