namespace SteamProject.ViewModels;

public class StatusViewModel
{
    public string GitSha { get; init; } = "unknown";
    public string BuildTimeUtc { get; init; } = "unknown";
    public string Environment { get; init; } = "";
    public bool SeedingOnStartup { get; init; }
    public bool StartupDataReady { get; init; }
}
