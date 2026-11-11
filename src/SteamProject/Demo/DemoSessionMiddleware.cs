using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;

namespace SteamProject.Demo;

/// <summary>
/// Keeps a demo session read only. Runs after authentication and before authorization, and only
/// acts on requests that carry the demo claim:
/// <list type="bullet">
/// <item>an expired demo session (or any demo session once the flag is off) is signed out and sent to the landing page;</item>
/// <item>every non-GET request is 403, except <c>POST /Demo/Exit</c>;</item>
/// <item>GET requests are allowed only for the database-backed pages listed in <see cref="AllowedPaths"/>
/// (Dashboard, Compete index and details, Library), so Admin, Manage, Inbox, Create, Initiate and
/// every page or API that would call Steam or OpenAI answer 403.</item>
/// </list>
/// Static files are served earlier in the pipeline and never reach this middleware.
/// </summary>
public class DemoSessionMiddleware
{
    private static readonly HashSet<string> AllowedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/",
        "/Home",
        "/Home/Index",
        "/Home/Privacy",
        "/Home/NotFound",
        "/Home/Error",
        "/Dashboard",
        "/Home/Dashboard",
        "/Compete",
        "/Compete/Index",
        "/Compete/Details",
        "/Library",
        "/Library/Index",
        "/Library/Recommendations",
        "/health",
        "/health/live",
        "/health/ready",
    };

    private static readonly string[] AllowedPrefixes =
    {
        "/Compete/Details/",
    };

    private readonly RequestDelegate _next;

    public DemoSessionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IConfiguration config)
    {
        if (!DemoMode.IsDemoSession(context.User))
        {
            await _next(context);
            return;
        }

        // A status-code re-execution (the friendly error page for a 403 set below) is let through.
        if (context.Features.Get<IStatusCodeReExecuteFeature>() is not null)
        {
            await _next(context);
            return;
        }

        if (!DemoMode.IsEnabled(config) || DemoMode.IsExpired(context.User, DateTimeOffset.UtcNow))
        {
            await context.SignOutAsync(IdentityConstants.ApplicationScheme);
            context.Response.Redirect("/");
            return;
        }

        // The persona may hold other roles in some environments (Development promotes the seeded
        // account to admin through AdminEmails). A demo session only ever acts as "demo".
        StripRolesOtherThanDemo(context.User);

        if (IsAllowed(context.Request.Method, context.Request.Path))
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
    }

    /// <summary>Whether a demo session may make this request.</summary>
    public static bool IsAllowed(string method, PathString path)
    {
        var p = Normalise(path);

        if (HttpMethods.IsPost(method))
            return string.Equals(p, "/Demo/Exit", StringComparison.OrdinalIgnoreCase);

        if (!HttpMethods.IsGet(method) && !HttpMethods.IsHead(method))
            return false;

        if (AllowedPaths.Contains(p)) return true;
        foreach (var prefix in AllowedPrefixes)
        {
            if (p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    public static void StripRolesOtherThanDemo(ClaimsPrincipal user)
    {
        foreach (var identity in user.Identities)
        {
            var extra = identity.FindAll(c => c.Type == identity.RoleClaimType && c.Value != DemoMode.RoleName).ToList();
            foreach (var claim in extra) identity.RemoveClaim(claim);
        }
    }

    private static string Normalise(PathString path)
    {
        var value = path.Value;
        if (string.IsNullOrEmpty(value)) return "/";
        return value.Length > 1 ? value.TrimEnd('/') : value;
    }
}
