using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using dddlib.Configuration;
using dddlib.Runtime;
using dddlib.Sdk.Configuration;

namespace dddlib.Tests.Support;

/// <summary>
/// Base class for feature scenarios. Each test runs against a fresh <see cref="Application"/> whose bootstrapper
/// provider finds the nested <see cref="IBootstrap{T}"/> classes declared alongside the scenario's subject types.
/// </summary>
[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "The application is disposed in the After(Test) hook.")]
public abstract class Feature
{
    private Application? application;

    /// <summary>
    /// Implemented by a class nested in a scenario to bootstrap the scenario's subject type <typeparamref name="T"/>.
    /// </summary>
    protected interface IBootstrap<T>
    {
        void Bootstrap(IConfiguration configure);
    }

    [Before(Test)]
    public void CreateApplication(TestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        this.application = new Application(new FeatureBootstrapperProvider());
        context.AddAsyncLocalValues();
    }

    [After(Test)]
    public void DisposeApplication()
    {
        this.application?.Dispose();
        this.application = null;
    }

    private sealed class FeatureBootstrapperProvider : IBootstrapperProvider
    {
        private readonly DefaultBootstrapperProvider fallback = new();

        public Action<IConfiguration> GetBootstrapper(Type type)
        {
            var bootstrapperType = type.DeclaringType?
                .GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(nestedType => nestedType.GetInterfaces().Any(
                    @interface => @interface.IsGenericType &&
                        @interface.GetGenericTypeDefinition() == typeof(IBootstrap<>) &&
                        @interface.GetGenericArguments()[0] == type));

            if (bootstrapperType is null)
            {
                return this.fallback.GetBootstrapper(type);
            }

            var bootstrapper = Activator.CreateInstance(bootstrapperType)!;
            var method = bootstrapperType.GetMethod("Bootstrap", [typeof(IConfiguration)])!;

            return (Action<IConfiguration>)Delegate.CreateDelegate(typeof(Action<IConfiguration>), bootstrapper, method);
        }
    }
}
