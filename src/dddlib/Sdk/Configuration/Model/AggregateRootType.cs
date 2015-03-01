// <copyright file="AggregateRootType.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk.Configuration.Model
{
    using System;
    using System.Globalization;
    using dddlib.Runtime;

    internal class AggregateRootType : EntityType
    {
        public AggregateRootType(Type runtimeType, ITypeAnalyzerService typeAnalyzerService, EntityType baseEntity)
            : base(runtimeType, typeAnalyzerService, baseEntity)
        {
            Guard.Against.Null(() => runtimeType);
            Guard.Against.Null(() => typeAnalyzerService);

            if (!typeAnalyzerService.IsValidAggregateRoot(runtimeType))
            {
                throw new BusinessException(
                    string.Format(CultureInfo.InvariantCulture, "The specified runtime type '{0}' is not an aggregate root.", runtimeType));
            }

            // NOTE (Cameron): Defaults.
            this.EventDispatcher = new DefaultEventDispatcher(runtimeType);
        }

        public Delegate UninitializedFactory { get; private set; }

        public IEventDispatcher EventDispatcher { get; private set; }

        // NOTE (Cameron): Only persist events if there is a way to reconstitute the persisted object.
        public bool PersistEvents
        {
            get { return this.UninitializedFactory != null; }
        }

        public void ConfigureUnititializedFactory(Delegate uninitializedFactory)
        {
            Guard.Against.Null(() => uninitializedFactory);

            // TODO (Cameron): Validate?
            this.UninitializedFactory = uninitializedFactory;
        }
    }
}
