using dddlib.Tests.Support;

namespace dddlib.Persistence.Tests.PublicApi;

// The approved public API of dddlib.Persistence. A change here is a deliberate API change: review the .received.txt
// file, then set DDDLIB_APPROVE_PUBLIC_API and rerun to approve it.
public class PublicApiTests
{
    [Test]
    public async Task PublicApiIsApproved()
    {
        var (approved, received) = global::dddlib.Tests.Support.PublicApi.Compare(typeof(IEventStoreRepository).Assembly, ApprovedPath());

        await Assert.That(received).IsEqualTo(approved);
    }

    private static string ApprovedPath() =>
        Path.Combine(global::dddlib.Tests.Support.PublicApi.ProjectDirectory("dddlib.Persistence.Tests.csproj"), "PublicApi", "dddlib.Persistence.approved.txt");
}
