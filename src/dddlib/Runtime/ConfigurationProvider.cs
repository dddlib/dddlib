// <copyright file="ConfigurationProvider.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;

    internal class ConfigurationProvider
    {
        public Configuration GetConfiguration(Assembly assembly)
        {
            var configuration = new Configuration();

            var bootstrapperTypes = assembly.GetTypes().Where(type => typeof(IBootstrapper).IsAssignableFrom(type));
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
                        assembly.GetName().Name));
            }

            var bootstrapperType = bootstrapperTypes.First();
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
                        "The bootstrapper of type '{0}' threw an exception during invocation.",
                        bootstrapperType.Name),
                    ex);
            }

            return configuration;
        }
    }
}
