// <copyright file="ValueObjectTypeFactory.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk.Configuration
{
    using System;
    using dddlib.Sdk.Configuration.Model;
    using dddlib.Sdk.Configuration.Model.BootstrapperService;

    internal class ValueObjectTypeFactory
    {
        private readonly ITypeAnalyzerService typeAnalyzerService;
        private readonly IBootstrapperProvider bootstrapperProvider;

        public ValueObjectTypeFactory(ITypeAnalyzerService typeAnalyzerService, IBootstrapperProvider bootstrapperProvider)
        {
            Guard.Against.Null(() => typeAnalyzerService);
            Guard.Against.Null(() => bootstrapperProvider);

            this.typeAnalyzerService = typeAnalyzerService;
            this.bootstrapperProvider = bootstrapperProvider;
        }
        
        public ValueObjectType Create(Type type)
        {
            var valueObjectType = new ValueObjectType(type, this.typeAnalyzerService);

            var configuration = new BootstrapperConfiguration(valueObjectType, this.typeAnalyzerService);
            var bootstrapper = this.bootstrapperProvider.GetBootstrapper(type);

            bootstrapper.Invoke(configuration);

            return valueObjectType;
        }
    }
}
