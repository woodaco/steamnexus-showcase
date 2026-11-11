namespace SteamProject.Helpers;

/// <summary>
/// Well-known sentinel identifiers for system-generated communication.
/// </summary>
public static class SystemUsers
{
    /// <summary>
    /// Sender id used for automated "SteamNexus" notifications (badge awards, competition
    /// system messages). It is intentionally not a real user row; the messaging UI renders it
    /// as the SteamNexus bot.
    /// </summary>
    public const int SystemSenderId = 69420;
}
