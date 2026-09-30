using dddlib.Persistence.EventDispatcher.SqlServer;

namespace dddlib.Persistence.EventDispatcher.Tests.PublicApi;

// The approved public APIs of dddlib.Persistence.EventDispatcher and dddlib.Persistence.EventDispatcher.SqlServer. A
// change here is a deliberate API change: review the .received.txt file, then set DDDLIB_APPROVE_PUBLIC_API and rerun
// to approve it.
public class PublicApiTests
{
    [Test]
    public async Task PublicApiIsApproved()
    {
        var (approved, received) = global::dddlib.Tests.Support.PublicApi.Compare(typeof(IEventDispatcher).Assembly, ApprovedPath("dddlib.Persistence.EventDispatcher"));

        await Assert.That(received).IsEqualTo(approved);
    }

    [Test]
    public async Task SqlServerPublicApiIsApproved()
    {
        var (approved, received) = global::dddlib.Tests.Support.PublicApi.Compare(typeof(SqlServerEventDispatcher).Assembly, ApprovedPath("dddlib.Persistence.EventDispatcher.SqlServer"));

        await Assert.That(received).IsEqualTo(approved);
    }

    private static string ApprovedPath(string assemblyName) =>
        Path.Combine(
            global::dddlib.Tests.Support.PublicApi.ProjectDirectory("dddlib.Persistence.EventDispatcher.Tests.csproj"),
            "PublicApi",
            assemblyName + ".approved.txt");
}
