using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SteamProject.Demo;

namespace SteamProject.Controllers;

/// <summary>
/// Entry and exit for the read-only demo persona. Enter is 404 unless
/// <c>Demo:Enabled</c> is true. Enter signs the seeded persona in with the normal Identity cookie
/// plus a <c>demo</c> claim holding the session's expiry; the cookie is not persistent and does
/// not slide, so the session ends after <see cref="DemoMode.SessionLength"/>.
/// </summary>
[AllowAnonymous]
[Route("Demo")]
public class DemoController : Controller
{
    private readonly IConfiguration _config;
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ILogger<DemoController> _logger;

    public DemoController(IConfiguration config, SignInManager<IdentityUser> signInManager,
        UserManager<IdentityUser> userManager, ILogger<DemoController> logger)
    {
        _config = config;
        _signInManager = signInManager;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpPost("Enter")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enter()
    {
        if (!DemoMode.IsEnabled(_config)) return NotFound();

        var persona = await _userManager.FindByEmailAsync(DemoMode.PersonaEmail);
        if (persona is null)
        {
            // Startup seeding can be skipped by an earlier seeder failing or by a flag flipped
            // after boot; seed on demand so the button works as soon as the flag is on.
            try
            {
                await DemoPersonaSeeder.ReseedAsync(HttpContext.RequestServices);
                persona = await _userManager.FindByEmailAsync(DemoMode.PersonaEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Seeding the demo persona on demand failed.");
            }
            if (persona is null)
            {
                _logger.LogWarning("Demo entry requested but the demo persona could not be seeded.");
                return StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
        }

        // Signing in replaces whatever application cookie the browser already had.
        var expires = DateTimeOffset.UtcNow.Add(DemoMode.SessionLength);
        var properties = new AuthenticationProperties
        {
            IsPersistent = false,
            IssuedUtc = DateTimeOffset.UtcNow,
            ExpiresUtc = expires,
            AllowRefresh = false,
        };
        await _signInManager.SignInWithClaimsAsync(persona, properties,
            new[] { new Claim(DemoMode.ClaimType, expires.ToUnixTimeSeconds().ToString()) });

        return Redirect("/Dashboard");
    }

    [HttpPost("Exit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Exit()
    {
        // Works even if the flag was turned off mid-session, so a demo visitor is never stuck.
        if (DemoMode.IsDemoSession(User))
            await _signInManager.SignOutAsync();

        return Redirect("/");
    }
}
