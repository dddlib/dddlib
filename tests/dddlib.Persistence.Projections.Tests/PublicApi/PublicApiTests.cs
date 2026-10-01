namespace dddlib.Persistence.Projections.Tests.PublicApi;

// The approved public APIs of dddlib.Persistence.Projections and dddlib.Persistence.Projections.SqlServer. A change
// here is a deliberate API change: review the .received.txt file, then set DDDLIB_APPROVE_PUBLIC_API and rerun to
// approve it.
public class PublicApiTests
{
    [Test]
    public async Task PublicApiIsApproved()
    {
        var (approved, received) = global::dddlib.Tests.Support.PublicApi.Compare(typeof(ProjectionRunner).Assembly, ApprovedPath("dddlib.Persistence.Projections"));

        await Assert.That(received).IsEqualTo(approved);
    }

    private static string ApprovedPath(string assemblyName) =>
        Path.Combine(
            global::dddlib.Tests.Support.PublicApi.ProjectDirectory("dddlib.Persistence.Projections.Tests.csproj"),
            "PublicApi",
            assemblyName + ".approved.txt");
}
