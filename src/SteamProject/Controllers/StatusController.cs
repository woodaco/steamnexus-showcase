using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SteamProject.Services;
using SteamProject.ViewModels;

namespace SteamProject.Controllers;

/// <summary>
/// Anonymous /status page: which build is running (short git SHA, build time) and in which
/// environment. It deliberately shows no configuration values, connection strings or keys.
/// </summary>
[AllowAnonymous]
public class StatusController : Controller
{
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;

    public StatusController(IWebHostEnvironment env, IConfiguration config)
    {
        _env = env;
        _config = config;
    }

    [HttpGet("/status")]
    public IActionResult Index()
    {
        var model = new StatusViewModel
        {
            GitSha = BuildInfo.GitSha,
            Environment = _env.EnvironmentName,
            SeedingOnStartup = StartupDataInitializer.ShouldSeed(_config),
            StartupDataReady = StartupDataInitializer.Ready,
        };
        Response.Headers.CacheControl = "no-store";
        return View(model);
    }
}
