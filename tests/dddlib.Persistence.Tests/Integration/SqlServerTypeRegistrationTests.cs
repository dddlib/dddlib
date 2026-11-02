using System.Collections.Concurrent;
using dddlib.Persistence.Sdk;
using dddlib.Persistence.SqlServer;
using dddlib.Tests.Support;
using Microsoft.Data.SqlClient;

namespace dddlib.Persistence.Tests.Integration;

// The type cache, the snapshot store and the natural key repository each register a type in the Types table the first
// time they store one. Concurrent first registrations of different types deadlocked on a fresh database
// (https://github.com/dddlib/dddlib/issues/55). Each round installs into a schema of its own, so its Types table starts
// empty, as on a freshly created database.
public class SqlServerTypeRegistrationTests : SqlServerIntegration
{
    private const int Rounds = 10;

    private static readonly Type[] Types =
    [
        typeof(Type01), typeof(Type02), typeof(Type03), typeof(Type04), typeof(Type05), typeof(Type06), typeof(Type07), typeof(Type08),
        typeof(Type09), typeof(Type10), typeof(Type11), typeof(Type12), typeof(Type13), typeof(Type14), typeof(Type15), typeof(Type16),
    ];

    [Test]
    public async Task RegistersATypeOnce()
    {
        var typeCache = new SqlServerTypeCache(this.ConnectionString);

        var typeId = await typeCache.GetTypeIdAsync(typeof(Type01));
        var sameTypeId = await new SqlServerTypeCache(this.ConnectionString).GetTypeIdAsync(typeof(Type01));

        await Assert.That(sameTypeId).IsEqualTo(typeId);
        await Assert.That(await typeCache.GetTypeAsync(typeId)).IsEqualTo(typeof(Type01));
    }

    [Test]
    public async Task ConcurrentFirstRegistrationsOfTheSameTypeAgree()
    {
        var schema = await this.CreateSchemaAsync();

        var typeIds = await Task.WhenAll(Enumerable.Range(0, Types.Length).Select(_ => new SqlServerTypeCache(this.ConnectionString, schema).GetTypeIdAsync(typeof(Type01))));

        await Assert.That(typeIds.Distinct()).Count().IsEqualTo(1);
    }

    [Test]
    public Task ConcurrentTypeCachesRegisteringDifferentTypesAllSucceed() =>
        this.RegisterConcurrentlyAsync((schema, type) => new SqlServerTypeCache(this.ConnectionString, schema).GetTypeIdAsync(type));

    [Test]
    public Task ConcurrentSnapshotsOfDifferentTypesAllSucceed() =>
        this.RegisterConcurrentlyAsync((schema, type) =>
            new SqlServerSnapshotStore(this.ConnectionString, schema).PutSnapshotAsync(Guid.NewGuid(), new Snapshot(1, Activator.CreateInstance(type)!)));

    [Test]
    public Task ConcurrentNaturalKeysOfDifferentAggregateRootTypesAllSucceed() =>
        this.RegisterConcurrentlyAsync((schema, type) =>
            new SqlServerNaturalKeyRepository(this.ConnectionString, schema).GetOrAddNaturalKeyAsync(type, "\"key\""));

    private async Task RegisterConcurrentlyAsync(Func<string, Type, Task> register)
    {
        var failures = new ConcurrentBag<Exception>();

        for (var round = 0; round < Rounds; round++)
        {
            var schema = await this.CreateSchemaAsync();

            await Task.WhenAll(Types.Select(async type =>
            {
                try
                {
                    await register(schema, type);
                }
                catch (Exception ex) when (ex is SqlException or PersistenceException)
                {
                    failures.Add(ex);
                }
            }));

            await Assert.That(failures.Select(static failure => failure.Message).Distinct()).IsEmpty();

            var typeIds = await Task.WhenAll(Types.Select(type => new SqlServerTypeCache(this.ConnectionString, schema).GetTypeIdAsync(type)));
            await Assert.That(typeIds.Distinct()).Count().IsEqualTo(Types.Length);
            await Assert.That(await this.Database.ExecuteScalarAsync($"SELECT COUNT(*) FROM [{schema}].[Types];")).IsEqualTo(Types.Length);
        }
    }

    private async Task<string> CreateSchemaAsync()
    {
        var schema = string.Concat("types_", Guid.NewGuid().ToString("N"));
        await this.Database.CreateSchemaAsync(schema);
        return schema;
    }

    private sealed record Type01;
    private sealed record Type02;
    private sealed record Type03;
    private sealed record Type04;
    private sealed record Type05;
    private sealed record Type06;
    private sealed record Type07;
    private sealed record Type08;
    private sealed record Type09;
    private sealed record Type10;
    private sealed record Type11;
    private sealed record Type12;
    private sealed record Type13;
    private sealed record Type14;
    private sealed record Type15;
    private sealed record Type16;
}
