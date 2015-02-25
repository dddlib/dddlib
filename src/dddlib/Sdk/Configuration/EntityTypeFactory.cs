// <copyright file="EntityTypeFactory.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk.Configuration
{
    using System;
    using System.Linq;
    using dddlib.Sdk.Configuration.Model;
    using dddlib.Sdk.Configuration.Model.BootstrapperService;

    internal class EntityTypeFactory
    {
        private readonly ITypeAnalyzerService typeAnalyzerService;
        private readonly IBootstrapperProvider bootstrapperProvider;

        public EntityTypeFactory(ITypeAnalyzerService typeAnalyzerService, IBootstrapperProvider bootstrapperProvider)
        {
            Guard.Against.Null(() => typeAnalyzerService);
            Guard.Against.Null(() => bootstrapperProvider);

            this.typeAnalyzerService = typeAnalyzerService;
            this.bootstrapperProvider = bootstrapperProvider;
        }

        public EntityType Create(Type type)
        {
            var entityType = default(EntityType);

            foreach (var subType in new[] { type }.Traverse(t => t.BaseType == typeof(object) ? null : new[] { t.BaseType }).Reverse())
            {
                if (entityType == null)
                {
                    entityType = new EntityType(subType, this.typeAnalyzerService);
                    continue;
                }

                entityType = new EntityType(subType, this.typeAnalyzerService, entityType);
            }

            var configuration = new BootstrapperConfiguration(entityType, this.typeAnalyzerService);
            var bootstrapper = this.bootstrapperProvider.GetBootstrapper(type);

            bootstrapper.Invoke(configuration);

            return entityType;
        }
    }
}
