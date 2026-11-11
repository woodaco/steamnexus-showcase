using System.Security.Claims;

namespace SteamProject.Demo;

/// <summary>
/// Shared constants and checks for the read-only demo persona (showcase milestone M6).
/// The demo is off unless <c>Demo:Enabled</c> is true. A demo session is an ordinary Identity
/// cookie sign-in of a seeded fictional user that also carries a <see cref="ClaimType"/> claim;
/// <see cref="DemoSessionMiddleware"/> keys every restriction off that claim, so a normal
/// password sign-in of the same seeded account is not affected.
/// </summary>
public static class DemoMode
{
    /// <summary>Configuration key that turns the demo on (default false).</summary>
    public const string EnabledKey = "Demo:Enabled";

    /// <summary>Claim added to the cookie of a demo session. Its value is the expiry as Unix seconds.</summary>
    public const string ClaimType = "demo";

    /// <summary>Role the seeded persona is placed in.</summary>
    public const string RoleName = "demo";

    /// <summary>The seeded fictional persona (also used by the fixture seeders and E2E specs).</summary>
    public const string PersonaEmail = "demo.player@example.com";
    public const string PersonaSteamName = "DemoPlayer";

    /// <summary>A demo session ends this long after it starts. It never slides.</summary>
    public static readonly TimeSpan SessionLength = TimeSpan.FromMinutes(60);

    /// <summary>Text of the banner shown on every page of a demo session.</summary>
    public const string BannerText = "You are browsing as a demo persona, read only";

    /// <summary>The persona's library is topped up to at least this many games on re-seed.</summary>
    public const int MinimumGames = 8;

    public static bool IsEnabled(IConfiguration config) => config.GetValue(EnabledKey, false);

    public static bool IsDemoSession(ClaimsPrincipal? user) =>
        user?.Identity?.IsAuthenticated == true && user.HasClaim(c => c.Type == ClaimType);

    /// <summary>True when the demo claim's expiry has passed (or cannot be read).</summary>
    public static bool IsExpired(ClaimsPrincipal user, DateTimeOffset now)
    {
        var value = user.FindFirst(ClaimType)?.Value;
        if (!long.TryParse(value, out var unixSeconds)) return true;
        return now >= DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
    }

    public static bool IsDemoRequest(IHttpContextAccessor accessor) => IsDemoSession(accessor.HttpContext?.User);
}
