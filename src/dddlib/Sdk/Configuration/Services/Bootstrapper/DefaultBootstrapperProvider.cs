// <copyright file="DefaultBootstrapperProvider.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk.Configuration.Services.Bootstrapper
{
    using System;
    using System.Diagnostics.CodeAnalysis;
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
        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1118:ParameterMustNotSpanMultipleLines", Justification = "It's fine here.")]
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
                        @"The assembly '{0}' has more than one bootstrapper defined. There can only be a single bootstrapper defined per assembly.
To fix this issue:
- ensure that there is only a single instance of a bootstrapper class declared in the assembly.",
                        type.Assembly.GetName()))
                {
                    HelpLink = "https://github.com/dddlib/dddlib/wiki/Bootstrapper",
                };
            }

            var bootstrapperType = bootstrapperTypes.First();
            if (bootstrapperType.GetConstructor(Type.EmptyTypes) == null)
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        @"The bootstrapper of type '{0}' cannot be instantiated as it does not have a default constructor.
To fix this issue:
- add a default constructor to the bootstrapper.",
                        bootstrapperType))
                {
                    HelpLink = "https://github.com/dddlib/dddlib/wiki/Bootstrapper",
                };
            }

            var bootstrapper = default(IBootstrapper);
            try
            {
                bootstrapper = (IBootstrapper)Activator.CreateInstance(bootstrapperType);
            }
            catch (RuntimeException)
            {
                throw;
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
