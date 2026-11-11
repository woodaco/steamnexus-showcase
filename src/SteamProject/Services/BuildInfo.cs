using System.Reflection;

namespace SteamProject.Services;

/// <summary>
/// What was deployed and when: the short git SHA and UTC build time stamped into the assembly at
/// build time (see the StampBuildInfo target in SteamProject.csproj). Contains nothing secret.
/// </summary>
public static class BuildInfo
{
    public static string GitSha { get; } = Read("BuildGitSha");
    public static string BuildTimeUtc { get; } = Read("BuildTimeUtc");

    private static string Read(string key) =>
        typeof(BuildInfo).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == key)?.Value is { Length: > 0 } value
            ? value
            : "unknown";
}
