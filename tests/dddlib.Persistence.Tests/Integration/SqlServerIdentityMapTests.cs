using dddlib.Persistence.SqlServer;
using dddlib.Tests.Support;

namespace dddlib.Persistence.Tests.Integration;

public class SqlServerIdentityMapTests : SqlServerIntegration
{
    [Test]
    public async Task TryGetWhenIdentityMapDoesNotContainNaturalKey()
    {
        var identityMap = new SqlServerIdentityMap(this.ConnectionString);
        var registration = new Registration(Guid.NewGuid().ToString("N"));

        var identity = await identityMap.TryGetAsync(typeof(Car), typeof(Registration), registration);

        await Assert.That(identity).IsNull();
    }

    [Test]
    public async Task TryGetWhenIdentityMapDoesContainNaturalKey()
    {
        var identityMap = new SqlServerIdentityMap(this.ConnectionString);
        var otherIdentityMap = new SqlServerIdentityMap(this.ConnectionString);
        var registration = new Registration(Guid.NewGuid().ToString("N"));

        var expectedIdentity = await identityMap.GetOrAddAsync(typeof(Car), typeof(Registration), registration);
        var actualIdentity = await otherIdentityMap.TryGetAsync(typeof(Car), typeof(Registration), registration);
        var sameIdentity = await otherIdentityMap.TryGetAsync(typeof(Car), typeof(Registration), registration);

        await Assert.That(actualIdentity).IsEqualTo(expectedIdentity);
        await Assert.That(sameIdentity).IsEqualTo(expectedIdentity);
    }

    [Test]
    public async Task GetOrAddWhenIdentityMapDoesNotContainNaturalKey()
    {
        var identityMap = new SqlServerIdentityMap(this.ConnectionString);
        var registration = new Registration(Guid.NewGuid().ToString("N"));

        var identity = await identityMap.GetOrAddAsync(typeof(Car), typeof(Registration), registration);
        var sameIdentity = await identityMap.GetOrAddAsync(typeof(Car), typeof(Registration), registration);

        await Assert.That(sameIdentity).IsEqualTo(identity);
    }

    [Test]
    public async Task GetOrAddWhenIdentityMapDoesContainNaturalKey()
    {
        var identityMap = new SqlServerIdentityMap(this.ConnectionString);
        var otherIdentityMap = new SqlServerIdentityMap(this.ConnectionString);
        var registration = new Registration(Guid.NewGuid().ToString("N"));

        var expectedIdentity = await identityMap.GetOrAddAsync(typeof(Car), typeof(Registration), registration);
        var actualIdentity = await otherIdentityMap.GetOrAddAsync(typeof(Car), typeof(Registration), registration);

        await Assert.That(actualIdentity).IsEqualTo(expectedIdentity);
    }

    [Test]
    public async Task RemoveFromIdentityMap()
    {
        var identityMap = new SqlServerIdentityMap(this.ConnectionString);
        var registration = new Registration(Guid.NewGuid().ToString("N"));

        var identity = await identityMap.GetOrAddAsync(typeof(Car), typeof(Registration), registration);
        await identityMap.RemoveAsync(identity);
        var otherIdentity = await identityMap.GetOrAddAsync(typeof(Car), typeof(Registration), registration);

        await Assert.That(otherIdentity).IsNotEqualTo(identity);
    }

    [Test]
    public async Task TryGetWhenIdentityMapHasRemovedNaturalKey()
    {
        var identityMap = new SqlServerIdentityMap(this.ConnectionString);
        var otherIdentityMap = new SqlServerIdentityMap(this.ConnectionString);
        var registration = new Registration(Guid.NewGuid().ToString("N"));

        var identity = await identityMap.GetOrAddAsync(typeof(Car), typeof(Registration), registration);
        var sameIdentity = await otherIdentityMap.GetOrAddAsync(typeof(Car), typeof(Registration), registration);
        await identityMap.RemoveAsync(identity);
        var removedIdentity = await otherIdentityMap.TryGetAsync(typeof(Car), typeof(Registration), registration);
        var stillRemovedIdentity = await otherIdentityMap.TryGetAsync(typeof(Car), typeof(Registration), registration);

        await Assert.That(sameIdentity).IsEqualTo(identity);
        await Assert.That(removedIdentity).IsNull();
        await Assert.That(stillRemovedIdentity).IsNull();
    }

    [Test]
    public async Task UseAlternateSchema()
    {
        await this.Database.CreateSchemaAsync("alternate");
        var identityMap = new SqlServerIdentityMap(this.ConnectionString, "alternate");
        var registration = new Registration(Guid.NewGuid().ToString("N"));

        var initialIdentity = await identityMap.TryGetAsync(typeof(Car), typeof(Registration), registration);
        var expectedIdentity = await identityMap.GetOrAddAsync(typeof(Car), typeof(Registration), registration);
        var subsequentIdentity = await identityMap.TryGetAsync(typeof(Car), typeof(Registration), registration);

        await Assert.That(initialIdentity).IsNull();
        await Assert.That(subsequentIdentity).IsEqualTo(expectedIdentity);
    }

    private sealed class Registration : ValueObject<Registration>
    {
        public Registration(string number)
        {
            ArgumentNullException.ThrowIfNull(number);

            this.Number = number;
        }

        public string Number { get; }
    }

    private sealed class Car : AggregateRoot
    {
        public Registration? Registration { get; private set; }
    }
}
