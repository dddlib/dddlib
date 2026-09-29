namespace dddlib.Persistence.EventDispatcher.Tests.PublicApi;

// The approved public API of dddlib.Persistence.EventDispatcher. A change here is a deliberate API change: review the
// .received.txt file, then set DDDLIB_APPROVE_PUBLIC_API and rerun to approve it.
public class PublicApiTests
{
    [Test]
    public async Task PublicApiIsApproved()
    {
        var approvedPath = Path.Combine(
            global::dddlib.Tests.Support.PublicApi.ProjectDirectory("dddlib.Persistence.EventDispatcher.Tests.csproj"),
            "PublicApi",
            "dddlib.Persistence.EventDispatcher.approved.txt");

        var (approved, received) = global::dddlib.Tests.Support.PublicApi.Compare(typeof(IEventDispatcher).Assembly, approvedPath);

        await Assert.That(received).IsEqualTo(approved);
    }
}
