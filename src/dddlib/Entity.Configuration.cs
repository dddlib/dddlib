// <copyright file="Entity.Configuration.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib
{
    using System;
    using dddlib.Sdk.Configuration.Model;

    /// <content>
    /// Represents an entity.
    /// </content>
    public abstract partial class Entity
    {
        internal Entity(dddlib.Sdk.Configuration.Model.NaturalKey naturalKey)
            : this(@this => new TypeInformation(naturalKey))
        {
        }

        private class TypeInformation
        {
            public TypeInformation(EntityType entityType)
            {
                Guard.Against.Null(() => entityType);

                this.GetNaturalKeyValue = entityType.NaturalKey == null
                    ? (Func<Entity, object>)null
                    : entity => entityType.NaturalKey.GetValue(entity);
            }

            public TypeInformation(dddlib.Sdk.Configuration.Model.NaturalKey naturalKey)
            {
                this.GetNaturalKeyValue = entity => naturalKey.GetValue(entity);
            }

            public bool HasNaturalKey
            {
                get { return this.GetNaturalKeyValue != null; }
            }

            public Func<Entity, object> GetNaturalKeyValue { get; private set; }
        }
    }
}
