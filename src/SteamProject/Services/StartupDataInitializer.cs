using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SteamProject.Areas.Identity.Data;
using SteamProject.Data;
using SteamProject.Utilities;
using SteamProject.Models;

namespace SteamProject.Services;

/// <summary>
/// Runs migrations and seeding in the background AFTER Kestrel is listening.
/// Azure App Service kills a container that has not opened its port within
/// WEBSITES_CONTAINER_START_TIME_LIMIT (230 s by default). On 2026-11-10 the synchronous
/// migrate + seed work in Program.cs took longer than that against a cold Azure SQL instance and
/// the site looped on "ContainerTimeout". Doing it here means the port opens in seconds and the
/// DB work takes as long as it takes. Requests that arrive before <see cref="Ready"/> is true may
/// see a not-yet-seeded database for a short while; every seeder is idempotent.
/// Migrations always run. Seeding can be turned off per environment with
/// <c>Seeding:RunOnStartup=false</c> (default true), so a cold start does only the migration check.
/// </summary>
public sealed class StartupDataInitializer : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _config;
    private readonly ILogger<StartupDataInitializer> _logger;

    public static bool Ready { get; private set; }

    /// <summary>Configuration key that turns startup seeding on or off (default on).</summary>
    public const string RunSeedingKey = "Seeding:RunOnStartup";

    /// <summary>True unless <c>Seeding:RunOnStartup</c> is set to false.</summary>
    public static bool ShouldSeed(IConfiguration config) => config.GetValue(RunSeedingKey, true);

    public StartupDataInitializer(IServiceProvider services, IConfiguration config, ILogger<StartupDataInitializer> logger)
    {
        _services = services;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Return control to the host immediately so the web server starts listening.
        await Task.Yield();

        using var scope = _services.CreateScope();
        var services = scope.ServiceProvider;
        var started = DateTime.UtcNow;
        try
        {
            await services.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync(stoppingToken);
            var dbContext = services.GetRequiredService<SteamInfoDbContext>();
            await dbContext.Database.MigrateAsync(stoppingToken);
            _logger.LogInformation("Database migrations applied in {Elapsed:N0} s.", (DateTime.UtcNow - started).TotalSeconds);

            var seed = ShouldSeed(_config);
            if (seed)
            {
                SeedGameData.Seed(dbContext);
                SeedDomainFixtures.Seed(dbContext);

                await SeedUsers.Initialize(services, SeedData.UserSeedData, _config["SeedUserPW"]);
                await SeedUsers.InitializeAdmin(services, "admin@example.com", "admin", _config["SeedAdminPW"], "My", "Admin");
            }
            else
            {
                _logger.LogInformation("Seeding skipped ({Key} is false); migrations only.", RunSeedingKey);
            }

            // Assign the admin role to configured admin emails.
            var adminEmails = _config.GetSection("AdminEmails").Get<string[]>() ?? Array.Empty<string>();
            if (adminEmails.Length > 0)
            {
                var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
                var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
                if (!await roleManager.RoleExistsAsync("admin"))
                {
                    await roleManager.CreateAsync(new IdentityRole("admin"));
                }
                foreach (var email in adminEmails)
                {
                    var user = await userManager.FindByEmailAsync(email);
                    if (user != null && !await userManager.IsInRoleAsync(user, "admin"))
                    {
                        await userManager.AddToRoleAsync(user, "admin");
                    }
                }
            }

            if (seed)
            {
                await SeedBadges.Initialize(services);
                SeedDomainFixtures.SeedDomainData(services);
            }


            _logger.LogInformation("Startup data initialisation finished in {Elapsed:N0} s.", (DateTime.UtcNow - started).TotalSeconds);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // host shutting down
        }
        catch (Exception ex)
        {
            // The app keeps running without the DB (CI / local dev without SQL Server); DB-backed
            // features fail on their own.
            _logger.LogWarning(ex, "Database migration/seeding skipped (app will run without DB).");
        }
        finally
        {
            // Independent of the seeders above: a failure there must not take the demo down with it.
            await ReseedDemoPersonaAsync(services);
            Ready = true;
        }
    }
    /// <summary>
    /// The read-only demo persona is re-seeded on every start while the demo is turned on.
    /// It runs in its own try so a failure here, or in the seeders before it, cannot hide the other.
    /// </summary>
    private async Task ReseedDemoPersonaAsync(IServiceProvider services)
    {
        if (!SteamProject.Demo.DemoMode.IsEnabled(_config)) return;
        try
        {
            await SteamProject.Demo.DemoPersonaSeeder.ReseedAsync(services);
            _logger.LogInformation("Demo persona seeded.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Seeding the demo persona failed; /Demo/Enter will retry on demand.");
        }
    }

}
