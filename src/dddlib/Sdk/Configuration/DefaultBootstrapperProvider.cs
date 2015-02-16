// <copyright file="DefaultBootstrapperProvider.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk.Configuration
{
    using System;
    using System.Globalization;
    using System.Linq;
    using dddlib.Configuration;
    using dddlib.Runtime;

    /// <summary>
    /// Represents the default bootstrapper provider.
    /// </summary>
    public class DefaultBootstrapperProvider : IBootstrapperProvider
    {
        /// <summary>
        /// Gets the bootstrapper for the specified type.
        /// </summary>
        /// <param name="type">The type to bootstrap.</param>
        /// <returns>The bootstrapper.</returns>
        //// TODO (Cameron): Caching.
        public Action<IConfiguration> GetBootstrapper(Type type)
        {
            var bootstrapperTypes = type.Assembly.GetTypes()
                .Where(assemblyType => typeof(IBootstrapper).IsAssignableFrom(assemblyType) && assemblyType != typeof(IBootstrapper));

            if (!bootstrapperTypes.Any())
            {
                return config => { };
            }

            if (bootstrapperTypes.Count() > 1)
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "The assembly '{0}' has more than one bootstrapper defined.",
                        type.Assembly.GetName()));
            }

            var bootstrapperType = bootstrapperTypes.First();
            if (bootstrapperType.GetConstructor(Type.EmptyTypes) == null)
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "The bootstrapper of type '{0}' cannot be instantiated as it does not have a default constructor.",
                        bootstrapperType));
            }

            var bootstrapper = default(IBootstrapper);
            try
            {
                bootstrapper = (IBootstrapper)Activator.CreateInstance(bootstrapperType);
            }
            catch (Exception ex)
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "The bootstrapper of type '{0}' threw an exception during instantiation.\r\nSee inner exception for details.",
                        bootstrapperType),
                    ex);
            }

            return bootstrapper.Bootstrap;
        }
    }
}
