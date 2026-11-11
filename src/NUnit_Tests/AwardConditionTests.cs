using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using SteamProject.DAL.Abstract;
using SteamProject.Models;
using SteamProject.Models.Awards.Concrete;

namespace NUnit_Tests
{
    /// <summary>
    /// Validates each badge award rule. A condition returns false if the user already holds the badge
    /// (mocked false here) OR the threshold is not met.
    /// </summary>
    public class AwardConditionTests
    {
        private Mock<IUserBadgeRepository> _badges = null!;

        [SetUp]
        public void Setup()
        {
            _badges = new Mock<IUserBadgeRepository>();
            _badges.Setup(r => r.UserHasBadgeAsync(It.IsAny<int>(), It.IsAny<string>())).ReturnsAsync(false);
        }

        private static User UserWith(
            string? steamId = null, int? level = null, int followedGames = 0, int friends = 0, int achievements = 0)
        {
            var u = new User { Id = 1, AspNetUserId = "x", SteamId = steamId, PlayerLevel = level };
            for (var i = 0; i < followedGames; i++) u.UserGameInfos.Add(new UserGameInfo { Followed = true });
            for (var i = 0; i < friends; i++) u.Friends.Add(new Friend { SteamId = $"f{i}" });
            for (var i = 0; i < achievements; i++) u.UserAchievements.Add(new UserAchievement());
            return u;
        }

        [Test]
        public async Task SteamLinked_true_with_steamid_false_without()
        {
            var c = new SteamAccountLinkedCondition();
            Assert.That(await c.IsFulfilledAsync(UserWith(steamId: "76561198000000001"), _badges.Object), Is.True);
            Assert.That(await c.IsFulfilledAsync(UserWith(steamId: null), _badges.Object), Is.False);
        }

        [Test]
        public async Task Level10_requires_level_10()
        {
            var c = new Level10Condition();
            Assert.That(await c.IsFulfilledAsync(UserWith(level: 10), _badges.Object), Is.True);
            Assert.That(await c.IsFulfilledAsync(UserWith(level: 9), _badges.Object), Is.False);
            Assert.That(await c.IsFulfilledAsync(UserWith(level: null), _badges.Object), Is.False);
        }

        [Test]
        public async Task DedicatedFollower_requires_three_followed_games()
        {
            var c = new FollowedThreeGamesCondition();
            Assert.That(await c.IsFulfilledAsync(UserWith(followedGames: 3), _badges.Object), Is.True);
            Assert.That(await c.IsFulfilledAsync(UserWith(followedGames: 2), _badges.Object), Is.False);
        }

        [Test]
        public async Task FriendlyFace_requires_five_friends()
        {
            var c = new FiveFriendsCondition();
            Assert.That(await c.IsFulfilledAsync(UserWith(friends: 5), _badges.Object), Is.True);
            Assert.That(await c.IsFulfilledAsync(UserWith(friends: 4), _badges.Object), Is.False);
        }

        [Test]
        public async Task AchievementHunter_requires_ten_achievements()
        {
            var c = new TenAchievementsCondition();
            Assert.That(await c.IsFulfilledAsync(UserWith(achievements: 10), _badges.Object), Is.True);
            Assert.That(await c.IsFulfilledAsync(UserWith(achievements: 9), _badges.Object), Is.False);
        }

        [Test]
        public async Task AlreadyHolding_the_badge_short_circuits_to_false()
        {
            _badges.Setup(r => r.UserHasBadgeAsync(It.IsAny<int>(), It.IsAny<string>())).ReturnsAsync(true);
            var c = new SteamAccountLinkedCondition();
            // Qualifies on merit, but already holds it -> not re-awarded.
            Assert.That(await c.IsFulfilledAsync(UserWith(steamId: "76561198000000001"), _badges.Object), Is.False);
        }
    }

    /// <summary>The Steam profile mapper must not throw on a private/invalid profile (empty players).</summary>
    public class SteamPocoMappingTests
    {
        [Test]
        public void TakeSteamPOCO_with_no_players_does_not_throw_and_leaves_user_blank()
        {
            var user = new User();
            var poco = new SteamProject.Models.DTO.SteamUserPOCO
            {
                response = new SteamProject.Models.DTO.Response
                {
                    players = new List<SteamProject.Models.DTO.Player>()
                }
            };
            Assert.DoesNotThrow(() => user.TakeSteamPOCO(poco));
            Assert.That(user.SteamId, Is.Null);
        }
    }
}
