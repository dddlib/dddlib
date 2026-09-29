using dddlib.Persistence.SqlServer;
using dddlib.Tests.Support;

namespace dddlib.Persistence.Tests.PublicApi;

// The approved public APIs of dddlib.Persistence and dddlib.Persistence.SqlServer. A change here is a deliberate API
// change: review the .received.txt file, then set DDDLIB_APPROVE_PUBLIC_API and rerun to approve it.
public class PublicApiTests
{
    [Test]
    public async Task PublicApiIsApproved()
    {
        var (approved, received) = global::dddlib.Tests.Support.PublicApi.Compare(typeof(IEventStoreRepository).Assembly, ApprovedPath("dddlib.Persistence"));

        await Assert.That(received).IsEqualTo(approved);
    }

    [Test]
    public async Task SqlServerPublicApiIsApproved()
    {
        var (approved, received) = global::dddlib.Tests.Support.PublicApi.Compare(typeof(SqlServerEventStore).Assembly, ApprovedPath("dddlib.Persistence.SqlServer"));

        await Assert.That(received).IsEqualTo(approved);
    }

    private static string ApprovedPath(string assemblyName) =>
        Path.Combine(global::dddlib.Tests.Support.PublicApi.ProjectDirectory("dddlib.Persistence.Tests.csproj"), "PublicApi", assemblyName + ".approved.txt");
}
