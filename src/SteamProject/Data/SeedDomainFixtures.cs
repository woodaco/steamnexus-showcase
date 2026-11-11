using System;
using System.Linq;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SteamProject.Models;

namespace SteamProject.Data
{
    /// <summary>
    /// Seeds reference + demo domain data the application depends on at runtime.
    /// Idempotent: safe to run on every startup. Used for local/dev validation against fixtures.
    /// </summary>
    public static class SeedDomainFixtures
    {
        // Status.Id is ValueGeneratedNever in the model, so ids must be assigned explicitly.
        private static readonly (int Id, string Name)[] CompetitionStatuses =
        {
            (1, "Active"), (2, "Ended"), (3, "Pending")
        };

        /// <summary>Seeds the Status reference table (required for competition create/end).</summary>
        public static void Seed(SteamInfoDbContext dbContext)
        {
            var existingNames = dbContext.Statuses.Select(s => s.Name).ToHashSet();
            var existingIds = dbContext.Statuses.Select(s => s.Id).ToHashSet();
            var added = false;
            foreach (var (id, name) in CompetitionStatuses)
            {
                if (existingNames.Contains(name) || existingIds.Contains(id)) continue;
                dbContext.Statuses.Add(new Status { Id = id, Name = name });
                existingIds.Add(id);
                added = true;
            }
            if (added) dbContext.SaveChanges();
        }

        /// <summary>
        /// Seeds demo relationships, owned games, and competitions between the seeded users so the
        /// social/competition features can be exercised against deterministic fixtures.
        /// Runs after users are seeded. Idempotent per-section.
        /// </summary>
        public static void SeedDomainData(IServiceProvider services)
        {
            var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
            var ctx = services.GetRequiredService<SteamInfoDbContext>();

            User? Domain(string email)
            {
                var idu = userManager.FindByEmailAsync(email).GetAwaiter().GetResult();
                return idu is null ? null : ctx.Users.FirstOrDefault(u => u.AspNetUserId == idu.Id);
            }

            var demoPlayer = Domain("demo.player@example.com");
            var testUser = Domain("TestUser@mail.com");
            var testUser2 = Domain("TestUser2@mail.com");
            if (demoPlayer is null) return; // users not seeded yet

            // 1) Ensure distinct, non-null SteamIds (seed data shipped demoPlayer/TestUser sharing one).
            const string demoPlayerSteam = "76561190000000101";
            const string testUserSteam = "76561198000000002";
            const string testUser2Steam = "76561198000000003";
            var profileChanged = false;
            if (demoPlayer.SteamId != demoPlayerSteam) { demoPlayer.SteamId = demoPlayerSteam; profileChanged = true; }
            if (demoPlayer.SteamName is null) { demoPlayer.SteamName = "DemoPlayer"; profileChanged = true; }
            if (demoPlayer.PlayerLevel is null) { demoPlayer.PlayerLevel = 12; profileChanged = true; } // qualifies "Rising Star"
            if (testUser is not null && (testUser.SteamId is null || testUser.SteamId == demoPlayerSteam))
            { testUser.SteamId = testUserSteam; testUser.SteamName ??= "TestUser"; profileChanged = true; }
            if (testUser2 is not null && testUser2.SteamId is null)
            { testUser2.SteamId = testUser2Steam; testUser2.SteamName ??= "TestUser2"; profileChanged = true; }
            if (profileChanged) ctx.SaveChanges();

            // 2) Friends for demoPlayer (and a reciprocal so messaging works both ways).
            if (!ctx.Friends.Any(f => f.RootId == demoPlayer.Id))
            {
                if (testUser is not null)
                    ctx.Friends.Add(new Friend { RootId = demoPlayer.Id, SteamId = testUser.SteamId!, SteamName = "TestUser", Linked = true, PersonaState = 1 });
                if (testUser2 is not null)
                    ctx.Friends.Add(new Friend { RootId = demoPlayer.Id, SteamId = testUser2.SteamId!, SteamName = "TestUser2", Linked = true, PersonaState = 0 });
                ctx.SaveChanges();
            }
            if (testUser2 is not null && !ctx.Friends.Any(f => f.RootId == testUser2.Id))
            {
                ctx.Friends.Add(new Friend { RootId = testUser2.Id, SteamId = demoPlayer.SteamId!, SteamName = "DemoPlayer", Linked = true, PersonaState = 1 });
                ctx.SaveChanges();
            }

            // 3) Owned games for demoPlayer (one followed, one hidden) so Library is exercisable.
            var games = ctx.Games.OrderBy(g => g.Id).Take(4).ToList();
            if (games.Count > 0 && !ctx.UserGameInfos.Any(u => u.OwnerId == demoPlayer.Id))
            {
                for (var i = 0; i < games.Count; i++)
                {
                    ctx.UserGameInfos.Add(new UserGameInfo
                    {
                        OwnerId = demoPlayer.Id,
                        GameId = games[i].Id,
                        PlayTime = (i + 1) * 120,
                        Followed = i == 0,
                        Hidden = i == 1
                    });
                }
                ctx.SaveChanges();
            }

            // 2b) Reset the demo user's friends to un-blocked on startup so the block/unblock tests start
            //     from a deterministic baseline (a crashed/interrupted run can leave friends blocked).
            var blockedFriends = ctx.Friends.Where(f => f.RootId == demoPlayer.Id && f.IsBlocked).ToList();
            if (blockedFriends.Any())
            {
                foreach (var f in blockedFriends) f.IsBlocked = false;
                ctx.SaveChanges();
            }

            // 3b) Give the friend (TestUser2) ownership of a game demoPlayer also owns, so the Compete/Create
            //     Duel flow has a deterministic SHARED game (games[0], which also gets achievements in step 5).
            if (testUser2 is not null && games.Count > 0 && !ctx.UserGameInfos.Any(u => u.OwnerId == testUser2.Id))
            {
                ctx.UserGameInfos.Add(new UserGameInfo { OwnerId = testUser2.Id, GameId = games[0].Id, PlayTime = 240 });
                ctx.SaveChanges();
            }

            // 4) An Active and an Ended competition created by demoPlayer (so Compete/vote are exercisable).
            if (games.Count > 0 && !ctx.Competitions.Any(c => c.CreatorId == demoPlayer.Id))
            {
                var now = DateTime.Now;
                var game = games[0];
                var active = new Competition { CreatorId = demoPlayer.Id, GameId = game.Id, StartDate = now.AddDays(-1), EndDate = now.AddDays(7), StatusId = 1 };
                var ended = new Competition { CreatorId = demoPlayer.Id, GameId = game.Id, StartDate = now.AddDays(-10), EndDate = now.AddDays(-1), StatusId = 2 };
                ctx.Competitions.Add(active);
                ctx.Competitions.Add(ended);
                ctx.SaveChanges();

                foreach (var comp in new[] { active, ended })
                {
                    ctx.CompetitionPlayers.Add(new CompetitionPlayer { CompetitionId = comp.Id, SteamId = demoPlayer.SteamId!, Score = 0 });
                    if (testUser2 is not null)
                        ctx.CompetitionPlayers.Add(new CompetitionPlayer { CompetitionId = comp.Id, SteamId = testUser2.SteamId!, Score = 0 });
                }
                ctx.SaveChanges();
            }

            // 5) Top demoPlayer up to qualify for EVERY badge condition (idempotent), so the award engine
            //    can be validated end-to-end: SteamId + level>=10 already set above; here ensure 5 friends,
            //    3 followed games, and 10 achievements.
            var friendCount = ctx.Friends.Count(f => f.RootId == demoPlayer.Id);
            for (var i = friendCount; i < 5; i++)
                ctx.Friends.Add(new Friend { RootId = demoPlayer.Id, SteamId = $"765611990000{i:D5}", SteamName = $"FixtureFriend{i}", PersonaState = 0 });
            if (friendCount < 5) ctx.SaveChanges();

            var followedCount = ctx.UserGameInfos.Count(u => u.OwnerId == demoPlayer.Id && u.Followed);
            if (followedCount < 3)
            {
                var toFollow = ctx.UserGameInfos
                    .Where(u => u.OwnerId == demoPlayer.Id && !u.Followed && !u.Hidden)
                    .Take(3 - followedCount).ToList();
                foreach (var ugi in toFollow) ugi.Followed = true;
                ctx.SaveChanges();
            }

            // 6) A dedicated demo banned SteamId (NOT used by any seeded user/friend) so the admin
            //    Unban round-trip can be validated off fixtures without nuking a real user's state.
            const string demoBannedSteamId = "76561190000000666";
            if (!ctx.BlackLists.Any(b => b.SteamId == demoBannedSteamId))
            {
                ctx.BlackLists.Add(new BlackList { SteamId = demoBannedSteamId });
                ctx.SaveChanges();
            }

            if (games.Count > 0 && ctx.UserAchievements.Count(a => a.OwnerId == demoPlayer.Id) < 10)
            {
                var achGame = games[0];
                var gameAchievements = ctx.GameAchievements.Where(a => a.GameId == achGame.Id).OrderBy(a => a.Id).ToList();
                while (gameAchievements.Count < 10)
                {
                    var idx = gameAchievements.Count;
                    var ga = new GameAchievement { GameId = achGame.Id, ApiName = $"FIXTURE_ACH_{idx}", DisplayName = $"Fixture Achievement {idx}", PointVal = 10 };
                    ctx.GameAchievements.Add(ga);
                    ctx.SaveChanges();
                    gameAchievements.Add(ga);
                }
                var have = ctx.UserAchievements.Count(a => a.OwnerId == demoPlayer.Id);
                for (var i = have; i < 10; i++)
                    ctx.UserAchievements.Add(new UserAchievement { OwnerId = demoPlayer.Id, AchievementId = gameAchievements[i].Id, Achieved = true, UnlockTime = DateTime.Now.AddDays(-i) });
                ctx.SaveChanges();
            }
        }
    }
}
