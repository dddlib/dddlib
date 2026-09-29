namespace dddlib.Tests;

public class SmokeTests
{
    [Test]
    public async Task TUnitRunsFromDotnetTest()
    {
        var sum = Add(1, 1);

        await Assert.That(sum).IsEqualTo(2);
    }

    private static int Add(int left, int right) => left + right;
}
