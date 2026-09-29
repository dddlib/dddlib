using System.Globalization;
using dddlib.Persistence.SqlServer;

namespace dddlib.Persistence.Tests.Unit;

public class SqlServerScriptTests
{
    private static readonly string[] ThreeBatches = ["SELECT 1;", "SELECT 2;", "SELECT 3;"];
    private static readonly string[] OneBatch = ["SELECT 'GO' AS [GO];" + Environment.NewLine + "GOTO Label;"];

    [Test]
    public async Task ScriptsStartAtVersionOneAndAreContiguous()
    {
        var versions = SqlServerSchemaInstaller.Scripts.Select(static script => script.Version).ToArray();

        await Assert.That(versions).IsEquivalentTo(Enumerable.Range(1, versions.Length));
        await Assert.That(SqlServerSchemaInstaller.RequiredVersion).IsEqualTo(versions.Length);
    }

    [Test]
    public async Task EveryScriptRecordsItsOwnVersion()
    {
        foreach (var script in SqlServerSchemaInstaller.Scripts)
        {
            var record = string.Format(CultureInfo.InvariantCulture, "INSERT INTO [dbo].[Versions] ([Version]) VALUES ({0});", script.Version);

            await Assert.That(script.Text).Contains(record);
            await Assert.That(SqlServerScript.SplitBatches(script.Text)[^1]).Contains(record);
        }
    }

    [Test]
    public async Task ScriptIsRewrittenForTheSchema()
    {
        var script = SqlServerSchemaInstaller.Scripts[0].For("alternate");

        await Assert.That(script).DoesNotContain("[dbo]");
        await Assert.That(script).Contains("CREATE TABLE [alternate].[Versions]");
    }

    [Test]
    public async Task ScriptRejectsAnInvalidSchema()
    {
        await Assert.That(() => SqlServerSchemaInstaller.Scripts[0].For("bad]name")).Throws<ArgumentException>();
    }

    [Test]
    public async Task SplitsOnGoLinesInAnyCase()
    {
        var batches = SqlServerScript.SplitBatches("SELECT 1;\r\nGO\r\nSELECT 2;\ngo\n  Go  \nSELECT 3;\nGO -- trailing comment\n");

        await Assert.That(batches).IsEquivalentTo(ThreeBatches);
    }

    [Test]
    public async Task DoesNotSplitOnGoWithinALine()
    {
        var batches = SqlServerScript.SplitBatches("SELECT 'GO' AS [GO];\nGOTO Label;\nGO\n");

        await Assert.That(batches).IsEquivalentTo(OneBatch);
    }

    [Test]
    public async Task RejectsGoWithARepeatCount()
    {
        await Assert.That(() => SqlServerScript.SplitBatches("INSERT INTO [T] DEFAULT VALUES;\nGO 2\n")).Throws<NotSupportedException>();
    }

    [Test]
    public async Task GetScriptCreatesTheSchemaFirst()
    {
        var script = SqlServerSchema.GetScript("alternate");
        var batches = SqlServerScript.SplitBatches(script);

        await Assert.That(batches[0]).Contains("CREATE SCHEMA [alternate]");
        await Assert.That(script).DoesNotContain("[dbo]");
    }
}
