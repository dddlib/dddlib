// <copyright file="DefaultTypeConfigurationProvider.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;

    /*  TODO (Cameron):
        Make method virtual.
        Consider removing RuntimeException - is this always invoked in a try catch that throws a runtime exception anyway?  */

    /// <summary>
    /// Represents the default type configuration provider.
    /// </summary>
    public class DefaultTypeConfigurationProvider : ITypeConfigurationProvider
    {
        private readonly Dictionary<Assembly, TypeConfiguration> typeConfigurations = new Dictionary<Assembly, TypeConfiguration>();

        /// <summary>
        /// Gets the configuration.
        /// </summary>
        /// <param name="type">The type to get the configuration for.</param>
        /// <returns>The configuration.</returns>
        public TypeConfiguration GetConfiguration(Type type)
        {
            Guard.Against.Null(() => type);

            var typeConfiguration = default(TypeConfiguration);
            if (this.typeConfigurations.TryGetValue(type.Assembly, out typeConfiguration))
            {
                return typeConfiguration;
            }

            this.typeConfigurations.Add(type.Assembly, typeConfiguration = CreateConfiguration(type));

            return typeConfiguration;
        }

        private static TypeConfiguration CreateConfiguration(Type type)
        {
            var typeConfiguration = new TypeConfiguration();

            var bootstrapperTypes = type.Assembly.GetTypes().Where(assemblyType => typeof(IBootstrapper).IsAssignableFrom(assemblyType));
            if (!bootstrapperTypes.Any())
            {
                return typeConfiguration;
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

            try
            {
                bootstrapper.Bootstrap(typeConfiguration);
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

            return typeConfiguration;
        }
    }
}
