using System.Reflection;
using PublicApiGenerator;

namespace dddlib.Tests.Support;

/// <summary>
/// Compares an assembly's public API with the approved snapshot checked in next to the test. Set the environment
/// variable <c>DDDLIB_APPROVE_PUBLIC_API</c> to rewrite the snapshot from the current build.
/// </summary>
public static class PublicApi
{
    private const string ApproveVariable = "DDDLIB_APPROVE_PUBLIC_API";

    /// <summary>
    /// Generates the API of the assembly and returns the approved and received texts, writing the received text
    /// next to the approved file when they differ (or approving it when asked to).
    /// </summary>
    public static (string Approved, string Received) Compare(Assembly assembly, string approvedPath)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrEmpty(approvedPath);

        var received = Normalize(assembly.GeneratePublicApi(new ApiGeneratorOptions { IncludeAssemblyAttributes = false }));
        var receivedPath = Path.ChangeExtension(approvedPath, ".received.txt");

        if (Environment.GetEnvironmentVariable(ApproveVariable) is not null)
        {
            File.WriteAllText(approvedPath, received);
            File.Delete(receivedPath);
            return (received, received);
        }

        var approved = File.Exists(approvedPath) ? Normalize(File.ReadAllText(approvedPath)) : string.Empty;
        if (approved == received)
        {
            File.Delete(receivedPath);
        }
        else
        {
            File.WriteAllText(receivedPath, received);
        }

        return (approved, received);
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
}
