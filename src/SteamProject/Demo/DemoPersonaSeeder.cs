using Microsoft.AspNetCore.Identity;
using SteamProject.Models;

namespace SteamProject.Demo;

/// <summary>
/// Re-seeds the fictional demo persona on startup when <c>Demo:Enabled</c> is true. Idempotent.
/// Puts the persona in the <c>demo</c> role, gives it a local avatar (so Library never asks Steam
/// for one), tops its library up to <see cref="DemoMode.MinimumGames"/> games from the seeded
/// catalogue and clears any blocked friends. The persona's Identity account is created without a
/// password when it does not exist yet, so the demo button is the only way into it.
/// </summary>
public static class DemoPersonaSeeder
{
    public const string PersonaSteamId = "76561190000000101";
    public const string PersonaAvatarUrl = "/assets/person.png";

    public static async Task ReseedAsync(IServiceProvider services)
    {
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var ctx = services.GetRequiredService<SteamInfoDbContext>();

        if (!await roleManager.RoleExistsAsync(DemoMode.RoleName))
            await roleManager.CreateAsync(new IdentityRole(DemoMode.RoleName));

        var identity = await userManager.FindByEmailAsync(DemoMode.PersonaEmail);
        if (identity is null)
        {
            identity = new IdentityUser
            {
                UserName = DemoMode.PersonaEmail,
                Email = DemoMode.PersonaEmail,
                EmailConfirmed = true,
            };
            var created = await userManager.CreateAsync(identity);
            if (!created.Succeeded)
                throw new InvalidOperationException("Could not create the demo persona: "
                    + string.Join("; ", created.Errors.Select(e => e.Description)));
        }
        if (!await userManager.IsInRoleAsync(identity, DemoMode.RoleName))
            await userManager.AddToRoleAsync(identity, DemoMode.RoleName);

        var persona = ctx.Users.FirstOrDefault(u => u.AspNetUserId == identity.Id);
        if (persona is null)
        {
            persona = new User { AspNetUserId = identity.Id };
            ctx.Users.Add(persona);
        }
        persona.SteamId ??= PersonaSteamId;
        persona.SteamName ??= DemoMode.PersonaSteamName;
        persona.AvatarUrl ??= PersonaAvatarUrl;
        if (persona.PlayerLevel is null or < 12) persona.PlayerLevel = 12;
        ctx.SaveChanges();

        // Library: at least MinimumGames owned games, most recently played first on the Dashboard.
        var owned = ctx.UserGameInfos.Where(u => u.OwnerId == persona.Id).Select(u => u.GameId).ToHashSet();
        if (owned.Count < DemoMode.MinimumGames)
        {
            var toAdd = ctx.Games
                .OrderBy(g => g.Id)
                .Where(g => !owned.Contains(g.Id))
                .Take(DemoMode.MinimumGames - owned.Count)
                .ToList();
            var now = DateTimeOffset.UtcNow;
            for (var i = 0; i < toAdd.Count; i++)
            {
                ctx.UserGameInfos.Add(new UserGameInfo
                {
                    OwnerId = persona.Id,
                    GameId = toAdd[i].Id,
                    PlayTime = 60 * (i + 3),
                    LastPlayed = (int)now.AddDays(-(i + 1)).ToUnixTimeSeconds(),
                });
            }
            if (toAdd.Count > 0) ctx.SaveChanges();
        }

        // Clear blocks a normal (password) session of the same seeded account may have left behind.
        var blocked = ctx.Friends.Where(f => f.RootId == persona.Id && f.IsBlocked).ToList();
        foreach (var f in blocked) f.IsBlocked = false;
        if (blocked.Count > 0) ctx.SaveChanges();
    }
}
