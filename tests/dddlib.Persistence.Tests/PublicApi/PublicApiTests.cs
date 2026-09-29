using System.Runtime.CompilerServices;
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

    private static string ApprovedPath([CallerFilePath] string sourcePath = "") =>
        Path.Combine(Path.GetDirectoryName(sourcePath)!, "dddlib.Persistence.approved.txt");
}
