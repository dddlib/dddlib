using dddlib.Configuration;

namespace dddlib.Tests;

// The assembly bootstrapper. Scenarios derived from Feature use their nested IBootstrap<T> classes instead; this
// one serves the plain unit and bug regression tests.
public sealed class Bootstrapper : IBootstrapper
{
    public void Bootstrap(IConfiguration configure)
    {
        configure.ValueObject<Bug.Bug0128.OtherSubject>().ToUseEqualityComparer(new Bug.Bug0128.OtherSubject.EqualityComparer());
    }
}
