// <copyright file="DefaultConfigurationProvider.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Globalization;
    using System.Linq;

    /*  TODO (Cameron): 
        Rename TypeConfigurationProvider.
        Make method virtual.
        Change exceptions to point to inner exceptions.
        Cache the results per assembly. eg. only make the bootstrapping calls once per assembly.
        Change exception arguments to type, not type.Name.  */

    /// <summary>
    /// Represents the default configuration provider.
    /// </summary>
    public class DefaultConfigurationProvider : IConfigurationProvider
    {
        /// <summary>
        /// Gets the configuration.
        /// </summary>
        /// <param name="type">The type to get the configuration for.</param>
        /// <returns>The configuration.</returns>
        public Configuration GetConfiguration(Type type)
        {
            Guard.Against.Null(() => type);

            var configuration = new Configuration();

            var bootstrapperTypes = type.Assembly.GetTypes().Where(assemblyType => typeof(IBootstrapper).IsAssignableFrom(assemblyType));
            if (!bootstrapperTypes.Any())
            {
                return configuration;
            }

            if (bootstrapperTypes.Count() > 1)
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture, 
                        "The assembly '{0}' has more than one bootstrapper defined.", 
                        type.Assembly.GetName().Name));
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
                        "The bootstrapper of type '{0}' threw an exception during instantiation.",
                        bootstrapperType.Name),
                    ex);
            }

            try
            {
                bootstrapper.Bootstrap(configuration);
            }
            catch (Exception ex)
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "The bootstrapper of type '{0}' threw an exception during invocation.\r\nSee inner exception for details.",
                        bootstrapperType),
                    ex);
            }

            return configuration;
        }
    }
}
