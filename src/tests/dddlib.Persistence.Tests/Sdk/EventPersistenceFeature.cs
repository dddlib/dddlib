// <copyright file="EventPersistenceFeature.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Tests.Sdk
{
    using Xbehave;

    public abstract class EventPersistenceFeature : Feature
    {
        private readonly IEventStoreRepository repository;

        public EventPersistenceFeature(IEventStoreRepository repository)
        {
            Guard.Against.Null(() => repository);

            this.repository = repository;
        }

        protected IEventStoreRepository Repository { get; private set; }

        [Background]
        public override void Background()
        {
            base.Background();

            "Given an event store repository"
                .f(() => this.Repository = this.repository);
        }

        /*
            1. can save new aggregate
            
         */
    }
}
