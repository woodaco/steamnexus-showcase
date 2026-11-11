using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace SteamProject.Utilities;

/// <summary>
/// Cookie-auth redirect handlers that answer API requests with status codes. Browsers that hit a
/// protected page still get the usual 302 to the login (or access-denied) page, but a request
/// whose path starts with <c>/api</c> gets 401 (not signed in) or 403 (signed in, not allowed),
/// which is what fetch/AJAX callers and API clients expect.
/// </summary>
public static class ApiAwareCookieEvents
{
    public static bool IsApiRequest(HttpRequest request) =>
        request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);

    public static Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context) =>
        RespondOrRedirect(context, StatusCodes.Status401Unauthorized);

    public static Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context) =>
        RespondOrRedirect(context, StatusCodes.Status403Forbidden);

    private static Task RespondOrRedirect(RedirectContext<CookieAuthenticationOptions> context, int apiStatusCode)
    {
        if (IsApiRequest(context.Request))
        {
            context.Response.StatusCode = apiStatusCode;
        }
        else
        {
            context.Response.Redirect(context.RedirectUri);
        }
        return Task.CompletedTask;
    }
}
