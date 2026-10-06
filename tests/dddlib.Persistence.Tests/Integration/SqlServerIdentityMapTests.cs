using System.Collections.Concurrent;
using dddlib.Persistence.Sdk;
using dddlib.Persistence.SqlServer;
using dddlib.Tests.Support;
using Microsoft.Data.SqlClient;

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
    public Task ConcurrentAddsAndRemovalsOfDifferentKeysAllSucceed() =>
        AddAndRemoveConcurrentlyAsync(() => new SqlServerIdentityMap(this.ConnectionString), typeof(Car), writers: 50);

    [Test]
    public Task ConcurrentAddsAndRemovalsOfKeysComparedInDotNetAllSucceed() =>
        AddAndRemoveConcurrentlyAsync(
            () => new DefaultIdentityMap(new SqlServerNaturalKeyRepository(this.ConnectionString), new ComparedInDotNetSerializer()),
            typeof(Truck),
            writers: 10);

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

    // Every natural key of a type shares the type's checkpoints, so writers of different keys contend for them. Each
    // test has an aggregate root type of its own: the tests share a database and run in parallel.
    private static async Task AddAndRemoveConcurrentlyAsync(Func<IIdentityMap> createIdentityMap, Type aggregateRootType, int writers)
    {
        var failures = new ConcurrentBag<Exception>();

        await Task.WhenAll(Enumerable.Range(0, writers).Select(async _ =>
        {
            var identityMap = createIdentityMap();
            for (var i = 0; i < 10; i++)
            {
                try
                {
                    var identity = await identityMap.GetOrAddAsync(aggregateRootType, typeof(Registration), new Registration(Guid.NewGuid().ToString("N")));
                    await identityMap.RemoveAsync(identity);
                }
                catch (Exception ex) when (ex is SqlException or PersistenceException)
                {
                    failures.Add(ex);
                }
            }
        }));

        await Assert.That(failures.Select(static failure => failure.Message).Distinct()).IsEmpty();
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

    // A serializer that leaves the comparison of keys to the identity map, as one for a key with custom equality does.
    private sealed class ComparedInDotNetSerializer : INaturalKeySerializer
    {
        private readonly DefaultNaturalKeySerializer serializer = new();

        public string Serialize(Type naturalKeyType, object naturalKey) => this.serializer.Serialize(naturalKeyType, naturalKey);

        public object Deserialize(Type naturalKeyType, string serializedNaturalKey) => this.serializer.Deserialize(naturalKeyType, serializedNaturalKey);
    }

    private class Car : AggregateRoot
    {
        public Registration? Registration { get; private set; }
    }

    private class Truck : AggregateRoot
    {
        public Registration? Registration { get; private set; }
    }
}
