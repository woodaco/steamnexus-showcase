using Microsoft.Extensions.Http;
using SteamProject.Models;
using SteamProject.Services;
using SteamProject.ViewModels;

namespace NUnit_Tests.ServiceTesting;

[TestFixture]
public class SteamServiceTests
{
    private const string TestApiKey = "test-api-key-12345";
    private const string TestAdminToken = "admin-token-67890";
    private const string TestCuratorSteamId = "76561198000000999";

    /// <summary>
    /// Build a SteamService wired to the fake handler.
    /// </summary>
    private static SteamService CreateService(FakeSteamHandler handler)
    {
        var factory = new FakeHttpClientFactory(handler);
        return new SteamService(
            TestApiKey,
            TestAdminToken,
            TestCuratorSteamId,
            factory,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SteamService>.Instance);
    }

    /// <summary>
    /// Simple IHttpClientFactory that always returns a client using the fake handler.
    /// </summary>
    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public FakeHttpClientFactory(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public HttpClient CreateClient(string name) => new(_handler);
    }

    // ==================== GetPlayerSummaries tests ====================

    [Test]
    public async Task GetSteamUserAsync_ReturnsParsedUser_WhenApiSucceeds()
    {
        var handler = new FakeSteamHandler();
        handler.RegisterFromEmbedded("GetPlayerSummaries", "player-summaries.json");

        var service = CreateService(handler);
        var user = await service.GetSteamUserAsync("76561198000000001");

        Assert.That(user.SteamId, Is.EqualTo("76561198000000001"));
        Assert.That(user.SteamName, Is.EqualTo("TestPlayerOne"));
    }

    [Test]
    public async Task GetSteamUserAsync_ReturnsEmptyUser_WhenApiFails()
    {
        var handler = new FakeSteamHandler();
        // No route registered — will return 404
        var service = CreateService(handler);
        var user = await service.GetSteamUserAsync("76561198000000099");

        Assert.That(user.SteamId, Is.EqualTo(null));
    }

    [Test]
    public async Task GetManyUsersAsync_ReturnsMappedUsers_WhenApiSucceeds()
    {
        var handler = new FakeSteamHandler();
        handler.RegisterFromEmbedded("GetPlayerSummaries", "player-summaries.json");

        var service = CreateService(handler);
        var users = await service.GetManyUsersAsync(new List<string> { "76561198000000001" });

        Assert.That(users.Count, Is.EqualTo(1));
        Assert.That(users[0].SteamId, Is.EqualTo("76561198000000001"));
        Assert.That(users[0].SteamName, Is.EqualTo("TestPlayerOne"));
    }

    [Test]
    public async Task GetManyUsersAsync_PreservesOrder_WhenApiOmitsSomePlayers()
    {
        var handler = new FakeSteamHandler();
        // Only return player 2, not player 1 — Steam does this for private profiles
        handler.RegisterFromEmbedded("GetPlayerSummaries", "player-summaries.json");

        var service = CreateService(handler);
        var users = await service.GetManyUsersAsync(new List<string> { "76561198000000001" });

        Assert.That(users.Count, Is.EqualTo(1));
        // Order preserved: first requested id maps to first result
        Assert.That(users[0].SteamId, Is.EqualTo("76561198000000001"));
    }

    [Test]
    public async Task GetManyUsersAsync_ReturnsBlankForMissingIds()
    {
        var handler = new FakeSteamHandler();
        // Only return player 2, request both 1 and 2
        handler.RegisterFromEmbedded("GetPlayerSummaries", "player-summaries.json");

        var service = CreateService(handler);
        var users = await service.GetManyUsersAsync(new List<string> { "76561198000000001", "76561198000000002" });

        Assert.That(users.Count, Is.EqualTo(2));
        // Player 1 found in response
        Assert.That(users[0].SteamId, Is.EqualTo("76561198000000001"));
        Assert.That(users[0].SteamName, Is.EqualTo("TestPlayerOne"));
        // Player 2 not in response (private profile) — blank but id preserved
        Assert.That(users[1].SteamId, Is.EqualTo("76561198000000002"));
    }

    [Test]
    public async Task GetManyUsersAsync_ReturnsEmptyList_WhenNoIdsProvided()
    {
        var handler = new FakeSteamHandler();
        var service = CreateService(handler);
        var users = await service.GetManyUsersAsync(new List<string>());

        Assert.That(users.Count, Is.EqualTo(0));
    }

    // ==================== GetUserLevel tests ====================

    [Test]
    public async Task GetUserLevelAsync_ReturnsLevel_WhenApiSucceeds()
    {
        var handler = new FakeSteamHandler();
        handler.RegisterFromEmbedded("GetSteamLevel", "user-level.json");

        var service = CreateService(handler);
        var level = await service.GetUserLevelAsync("76561198000000001");

        Assert.That(level, Is.EqualTo(42));
    }

    [Test]
    public async Task GetUserLevelAsync_ReturnsZero_WhenApiFails()
    {
        var handler = new FakeSteamHandler();
        // No route — returns 404
        var service = CreateService(handler);
        var level = await service.GetUserLevelAsync("76561198000000099");

        Assert.That(level, Is.EqualTo(0));
    }

    // ==================== GetOwnedGames tests ====================

    [Test]
    public async Task GetGamesAsync_ReturnsParsedGames_WhenApiSucceeds()
    {
        var handler = new FakeSteamHandler();
        handler.RegisterFromEmbedded("GetOwnedGames", "library-owned-games.json");

        var service = CreateService(handler);
        var games = (await service.GetGamesAsync("76561198000000001", 1)).ToList();

        Assert.That(games.Count, Is.EqualTo(3));
        // Games should be sorted alphabetically by name
        Assert.That(games[0].Name, Is.EqualTo("Counter-Strike 2"));
        Assert.That(games[1].Name, Is.EqualTo("Dota 2"));
        Assert.That(games[2].Name, Is.EqualTo("Team Fortress 2"));
    }

    [Test]
    public async Task GetGamesAsync_ReturnsEmpty_WhenApiFails()
    {
        var handler = new FakeSteamHandler();
        // No route — returns 404
        var service = CreateService(handler);
        var games = (await service.GetGamesAsync("76561198000000099", 1)).ToList();

        Assert.That(games.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task GetGamesAsync_ReturnsEmpty_WhenNoGamesInResponse()
    {
        var handler = new FakeSteamHandler();
        // Empty games array
        handler.Register("GetOwnedGames", 200, "{\"response\":{\"games\":[]}}");

        var service = CreateService(handler);
        var games = (await service.GetGamesAsync("76561198000000001", 1)).ToList();

        Assert.That(games.Count, Is.EqualTo(0));
    }

    // ==================== GetAchievements tests ====================

    [Test]
    public async Task GetAchievementsAsync_ReturnsParsedAchievements_WhenApiSucceeds()
    {
        var handler = new FakeSteamHandler();
        handler.RegisterFromEmbedded("GetPlayerAchievements", "achievements.json");

        var service = CreateService(handler);
        var result = await service.GetAchievementsAsync("76561198000000001", 730);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.playerstats, Is.Not.Null);
        Assert.That(result.playerstats.achievements, Is.Not.Null);
        Assert.That(result.playerstats.achievements.Count, Is.EqualTo(3));
    }

    [Test]
    public async Task GetAchievementsAsync_ReturnsNull_WhenApiFails()
    {
        var handler = new FakeSteamHandler();
        // No route — returns 404
        var service = CreateService(handler);
        var result = await service.GetAchievementsAsync("76561198000000099", 730);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetAchievementsAsync_ReturnsNull_WhenMalformedJson()
    {
        var handler = new FakeSteamHandler();
        // Malformed JSON — missing closing brace
        handler.Register("GetPlayerAchievements", 200, "{\"playerstats\":{\"achievements\":[{");

        var service = CreateService(handler);
        var result = await service.GetAchievementsAsync("76561198000000001", 730);

        Assert.That(result, Is.Null);
    }

    // ==================== GetSchema tests ====================

    [Test]
    public async Task GetSchemaAsync_ReturnsParsedSchema_WhenApiSucceeds()
    {
        var handler = new FakeSteamHandler();
        handler.RegisterFromEmbedded("GetSchemaForGame", "schema.json");

        var service = CreateService(handler);
        var result = await service.GetSchemaAsync(730);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.game, Is.Not.Null);
        Assert.That(result.game.gameName, Is.EqualTo("Counter-Strike 2"));
        Assert.That(result.game.availableGameStats.achievements.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task GetSchemaAsync_ReturnsNull_WhenApiFails()
    {
        var handler = new FakeSteamHandler();
        // No route — returns 404
        var service = CreateService(handler);
        var result = await service.GetSchemaAsync(999);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetSchemaAsync_ReturnsNull_WhenMalformedJson()
    {
        var handler = new FakeSteamHandler();
        // Malformed JSON — unterminated string
        handler.Register("GetSchemaForGame", 200, "{\"game\":{\"gameName\":\"CS2\",\"availableGameStats\":{\"achievements\":[{");

        var service = CreateService(handler);
        var result = await service.GetSchemaAsync(730);

        Assert.That(result, Is.Null);
    }

    // ==================== GetGlobalAchievementPercentages tests ====================

    [Test]
    public async Task GetGAPAsync_ReturnsParsedPercentages_WhenApiSucceeds()
    {
        var handler = new FakeSteamHandler();
        handler.RegisterFromEmbedded("GetGlobalAchievementPercentagesForApp", "global-achievements.json");

        var service = CreateService(handler);
        var result = await service.GetGAPAsync(730);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.achievementpercentages, Is.Not.Null);
        Assert.That(result.achievementpercentages.achievements.Count, Is.EqualTo(3));
    }

    [Test]
    public async Task GetGAPAsync_ReturnsNull_WhenApiFails()
    {
        var handler = new FakeSteamHandler();
        // No route — returns 404
        var service = CreateService(handler);
        var result = await service.GetGAPAsync(999);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetGAPAsync_ReturnsNull_WhenMalformedJson()
    {
        var handler = new FakeSteamHandler();
        // Malformed JSON — missing closing brace
        handler.Register("GetGlobalAchievementPercentagesForApp", 200, "{\"achievementpercentages\":{\"achievements\":[{");

        var service = CreateService(handler);
        var result = await service.GetGAPAsync(730);

        Assert.That(result, Is.Null);
    }

    // ==================== Error path tests (429, 500) ====================

    [Test]
    public async Task GetSteamUserAsync_ReturnsEmpty_WhenApiReturns429()
    {
        var handler = new FakeSteamHandler();
        handler.Register("GetPlayerSummaries", 429, "{\"error\":\"rate limit exceeded\"}");

        var service = CreateService(handler);
        var user = await service.GetSteamUserAsync("76561198000000001");

        Assert.That(user.SteamId, Is.EqualTo(null));
    }

    [Test]
    public async Task GetUserLevelAsync_ReturnsZero_WhenApiReturns500()
    {
        var handler = new FakeSteamHandler();
        handler.Register("GetSteamLevel", 500, "{\"error\":\"internal server error\"}");

        var service = CreateService(handler);
        var level = await service.GetUserLevelAsync("76561198000000001");

        Assert.That(level, Is.EqualTo(0));
    }

    [Test]
    public async Task GetGamesAsync_ReturnsEmpty_WhenApiReturns429()
    {
        var handler = new FakeSteamHandler();
        handler.Register("GetOwnedGames", 429, "{\"error\":\"rate limit exceeded\"}");

        var service = CreateService(handler);
        var games = (await service.GetGamesAsync("76561198000000001", 1)).ToList();

        Assert.That(games.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task GetAchievementsAsync_ReturnsNull_WhenApiReturns500()
    {
        var handler = new FakeSteamHandler();
        handler.Register("GetPlayerAchievements", 500, "{\"error\":\"internal server error\"}");

        var service = CreateService(handler);
        var result = await service.GetAchievementsAsync("76561198000000001", 730);

        Assert.That(result, Is.Null);
    }

    // ==================== GetGameInfo tests (store.steampowered.com) ====================

    [Test]
    public async Task GetGameInfoAsync_ReturnsParsedVM_WhenApiSucceeds()
    {
        var handler = new FakeSteamHandler();
        handler.RegisterFromEmbedded("appdetails", "game-info.json");

        var service = CreateService(handler);
        var game = new Game { AppId = 440 };
        var vm = await service.GetGameInfoAsync(game);

        Assert.That(vm._poco, Is.Not.Null);
        Assert.That(vm._poco.response.data.short_description, Is.EqualTo("Nine classes of chaos."));
    }

    [Test]
    public async Task GetGameInfoAsync_ReturnsEmptyVM_WhenApiFails()
    {
        var handler = new FakeSteamHandler();
        // No route — returns 404
        var service = CreateService(handler);
        var game = new Game { AppId = 999 };
        var vm = await service.GetGameInfoAsync(game);

        Assert.That(vm._poco, Is.Null);
    }

    [Test]
    public async Task GetGameInfoAsync_ReturnsEmptyVM_WhenMalformedJson()
    {
        var handler = new FakeSteamHandler();
        // Malformed JSON — missing closing brace
        handler.Register("appdetails", 200, "{\"response\":{\"440\":{\"success\":true,\"data\":{\"name\":\"TF2\"}}");

        var service = CreateService(handler);
        var game = new Game { AppId = 440 };
        var vm = await service.GetGameInfoAsync(game);

        Assert.That(vm._poco, Is.Null);
    }

    // ==================== ExtractGenres/Categories tests (pure methods) ====================

    [Test]
    public async Task ExtractGenresFromGameInfo_ReturnsParsedGenres()
    {
        var handler = new FakeSteamHandler();
        handler.RegisterFromEmbedded("appdetails", "game-info.json");

        var service = CreateService(handler);
        var game = new Game { AppId = 440 };
        var vm = await service.GetGameInfoAsync(game);

        var genres = service.ExtractGenresFromGameInfo(vm);

        Assert.That(genres, Is.Not.Null);
        Assert.That(genres.Count, Is.EqualTo(2));
        Assert.That(genres.Contains("Action"));
        Assert.That(genres.Contains("Free to Play"));
    }

    [Test]
    public async Task ExtractCategoriesFromGameInfo_ReturnsParsedCategories()
    {
        var handler = new FakeSteamHandler();
        handler.RegisterFromEmbedded("appdetails", "game-info.json");

        var service = CreateService(handler);
        var game = new Game { AppId = 440 };
        var vm = await service.GetGameInfoAsync(game);

        var categories = service.ExtractCategoriesFromGameInfo(vm);

        Assert.That(categories, Is.Not.Null);
        Assert.That(categories.Count, Is.EqualTo(2));
        Assert.That(categories.Contains("Multi-player"));
        Assert.That(categories.Contains("Online Multi-Player"));
    }

    [Test]
    public void ExtractGenresFromGameInfo_ReturnsEmpty_WhenNoData()
    {
        var service = CreateService(new FakeSteamHandler());
        var vm = new GameVM(); // _poco is null

        var genres = service.ExtractGenresFromGameInfo(vm);

        Assert.That(genres.Count, Is.EqualTo(0));
    }

    [Test]
    public void ExtractCategoriesFromGameInfo_ReturnsEmpty_WhenNoData()
    {
        var service = CreateService(new FakeSteamHandler());
        var vm = new GameVM(); // _poco is null

        var categories = service.ExtractCategoriesFromGameInfo(vm);

        Assert.That(categories.Count, Is.EqualTo(0));
    }

    // ==================== Integration: no live API calls ====================

    [Test]
    public async Task AllEndpoints_UseFakeHandler_WithNoLiveCalls()
    {
        var handler = new FakeSteamHandler();
        handler.RegisterFromEmbedded("GetPlayerSummaries", "player-summaries.json");
        handler.RegisterFromEmbedded("GetOwnedGames", "library-owned-games.json");
        handler.RegisterFromEmbedded("GetSteamLevel", "user-level.json");
        handler.RegisterFromEmbedded("GetPlayerAchievements", "achievements.json");
        handler.RegisterFromEmbedded("GetSchemaForGame", "schema.json");
        handler.RegisterFromEmbedded("GetGlobalAchievementPercentagesForApp", "global-achievements.json");
        handler.RegisterFromEmbedded("appdetails", "game-info.json");

        var service = CreateService(handler);

        // Run all endpoints — none should hit the network
        var user = await service.GetSteamUserAsync("76561198000000001");
        Assert.That(user.SteamName, Is.EqualTo("TestPlayerOne"));

        var games = (await service.GetGamesAsync("76561198000000001", 1)).ToList();
        Assert.That(games.Count, Is.EqualTo(3));

        var level = await service.GetUserLevelAsync("76561198000000001");
        Assert.That(level, Is.EqualTo(42));

        var achievements = await service.GetAchievementsAsync("76561198000000001", 730);
        Assert.That(achievements, Is.Not.Null);

        var schema = await service.GetSchemaAsync(730);
        Assert.That(schema, Is.Not.Null);

        var gap = await service.GetGAPAsync(730);
        Assert.That(gap, Is.Not.Null);

        var gameInfo = await service.GetGameInfoAsync(new Game { AppId = 440 });
        Assert.That(gameInfo._poco, Is.Not.Null);

        // If we get here without exceptions or nulls where not expected, all endpoints work with the fake handler.
    }

    // ==================== GetGameNews tests ====================

    [Test]
    public async Task GetGameNewsAsync_ReturnsFilteredNews_WhenApiSucceeds()
    {
        var handler = new FakeSteamHandler();
        handler.RegisterFromEmbedded("GetNewsForApp", "game-news.json");

        var service = CreateService(handler);
        var game = new Game { AppId = 730 };
        var newsVM = await service.GetGameNewsAsync(game, count: 5);

        Assert.That(newsVM._poco, Is.Not.Null);
        // SteamDb feed should be filtered out
        Assert.That(newsVM._poco.appnews.newsitems.Count, Is.EqualTo(2));
        Assert.That(newsVM._poco.appnews.newsitems[0].feedlabel, Is.Not.EqualTo("SteamDb"));
    }

    [Test]
    public async Task GetGameNewsAsync_ReturnsEmptyVM_WhenApiFails()
    {
        var handler = new FakeSteamHandler();
        // No route — returns 404
        var service = CreateService(handler);
        var game = new Game { AppId = 999 };
        var newsVM = await service.GetGameNewsAsync(game, count: 5);

        Assert.That(newsVM._poco, Is.Null);
    }

    [Test]
    public async Task GetGameNewsAsync_RespectsCountLimit()
    {
        var handler = new FakeSteamHandler();
        handler.RegisterFromEmbedded("GetNewsForApp", "game-news.json");

        var service = CreateService(handler);
        var game = new Game { AppId = 730 };
        // Request only 1 news item (service fetches 2 + count*2 = 4)
        var newsVM = await service.GetGameNewsAsync(game, count: 1);

        Assert.That(newsVM._poco, Is.Not.Null);
        Assert.That(newsVM._poco.appnews.newsitems.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task GetGameNewsAsync_ReturnsEmptyVM_WhenMalformedJson()
    {
        var handler = new FakeSteamHandler();
        // Malformed JSON — missing closing brace
        handler.Register("GetNewsForApp", 200, "{\"appnews\":{\"newsitems\":[{");

        var service = CreateService(handler);
        var game = new Game { AppId = 730 };
        var newsVM = await service.GetGameNewsAsync(game, count: 5);

        Assert.That(newsVM._poco, Is.Null);
    }

    // ==================== GetFriendsList tests ====================

    [Test]
    public async Task GetFriendsListAsync_ReturnsSortedFriends_WhenApiSucceeds()
    {
        var handler = new FakeSteamHandler();
        // Register GetFriendList first (insertion order matters for prefix matching)
        handler.RegisterFromEmbedded("GetFriendList", "friends-list.json");
        // The service makes a second call to GetPlayerSummaries for friend details
        handler.RegisterFromEmbedded("GetPlayerSummaries", "friends-player-summaries.json");

        var service = CreateService(handler);
        var friends = await service.GetFriendsListAsync("76561198000000001", 1);

        Assert.That(friends.Count, Is.EqualTo(2));
        // Friends should be sorted alphabetically by SteamName
        Assert.That(friends[0].SteamName, Is.EqualTo("TestPlayerThree"));
        Assert.That(friends[1].SteamName, Is.EqualTo("TestPlayerTwo"));
    }

    [Test]
    public async Task GetFriendsListAsync_ReturnsEmpty_WhenApiFails()
    {
        var handler = new FakeSteamHandler();
        // No route — returns 404
        var service = CreateService(handler);
        var friends = await service.GetFriendsListAsync("76561198000000099", 1);

        Assert.That(friends.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task GetFriendsListAsync_ReturnsEmpty_WhenNoFriendsInResponse()
    {
        var handler = new FakeSteamHandler();
        // Empty friends array
        handler.Register("GetFriendList", 200, "{\"friendslist\":{\"friends\":[]}}");

        var service = CreateService(handler);
        var friends = await service.GetFriendsListAsync("76561198000000001", 1);

        Assert.That(friends.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task GetFriendSpecificAsync_ReturnsFriend_WhenFound()
    {
        var handler = new FakeSteamHandler();
        handler.RegisterFromEmbedded("GetFriendList", "friends-list.json");
        // Friend 002 is in friends-player-summaries.json
        handler.RegisterFromEmbedded("GetPlayerSummaries", "friends-player-summaries.json");

        var service = CreateService(handler);
        var friend = await service.GetFriendSpecificAsync("76561198000000001", 1, "76561198000000002");

        Assert.That(friend, Is.Not.Null);
        Assert.That(friend.SteamId, Is.EqualTo("76561198000000002"));
    }

    [Test]
    public async Task GetFriendSpecificAsync_ReturnsNull_WhenNotFound()
    {
        var handler = new FakeSteamHandler();
        handler.RegisterFromEmbedded("GetFriendList", "friends-list.json");
        handler.RegisterFromEmbedded("GetPlayerSummaries", "player-summaries.json");

        var service = CreateService(handler);
        var friend = await service.GetFriendSpecificAsync("76561198000000001", 1, "76561198000000099");

        Assert.That(friend, Is.Null);
    }

    // ==================== GetSharedGames tests ====================

    [Test]
    public async Task GetSharedGamesAsync_ReturnsCommonGames_WhenApiSucceeds()
    {
        var handler = new FakeSteamHandler();
        // Both calls go to GetOwnedGames endpoint
        handler.RegisterFromEmbedded("GetOwnedGames", "library-owned-games.json");

        var service = CreateService(handler);
        var sharedGames = (await service.GetSharedGamesAsync("76561198000000001", "76561198000000002", 1)).ToList();

        // Both users have the same games in the fixture, so all should be shared
        Assert.That(sharedGames.Count, Is.EqualTo(3));
    }

    [Test]
    public async Task GetSharedGamesAsync_ReturnsEmpty_WhenNoCommonGames()
    {
        var handler = new FakeSteamHandler();
        // User 1 has games, user 2 has different games
        handler.Register("GetOwnedGames", 200, "{\"response\":{\"games\":[{\"appid\":730,\"name\":\"CS2\"}]}}");

        var service = CreateService(handler);
        var sharedGames = (await service.GetSharedGamesAsync("76561198000000001", "76561198000000002", 1)).ToList();

        // Both have only CS2, so it should be shared
        Assert.That(sharedGames.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task GetSharedGamesAsync_ReturnsEmpty_WhenApiFails()
    {
        var handler = new FakeSteamHandler();
        // No route — returns 404 for both calls
        var service = CreateService(handler);
        var sharedGames = (await service.GetSharedGamesAsync("76561198000000001", "76561198000000002", 1)).ToList();

        Assert.That(sharedGames.Count, Is.EqualTo(0));
    }

    // ==================== GetSharedMissingAchievements tests ====================

    [Test]
    public async Task GetSharedMissingAchievementsAsync_ReturnsCommonUnachieved_WhenApiSucceeds()
    {
        var handler = new FakeSteamHandler();
        // Both users have the same achievements, both missing the first two
        handler.RegisterFromEmbedded("GetPlayerAchievements", "shared-missing-achievements.json");

        var service = CreateService(handler);
        var missing = await service.GetSharedMissingAchievementsAsync(
            "76561198000000001", "76561198000000002", 730);

        Assert.That(missing.Count, Is.EqualTo(2));
        // Should be sorted alphabetically by name
        Assert.That(missing[0].name, Is.EqualTo("Defuse Close Call"));
        Assert.That(missing[1].name, Is.EqualTo("Pistol Round Winner"));
    }

    [Test]
    public async Task GetSharedMissingAchievementsAsync_ReturnsEmpty_WhenAllAchieved()
    {
        var handler = new FakeSteamHandler();
        // Both users have achieved everything
        handler.Register("GetPlayerAchievements", 200, "{\"playerstats\":{\"achievements\":[{\"apiname\":\"ACH1\",\"achieved\":1}]}}");

        var service = CreateService(handler);
        var missing = await service.GetSharedMissingAchievementsAsync(
            "76561198000000001", "76561198000000002", 730);

        Assert.That(missing.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task GetSharedMissingAchievementsAsync_ReturnsEmpty_WhenApiFails()
    {
        var handler = new FakeSteamHandler();
        // No route — returns 404 for both calls
        var service = CreateService(handler);
        var missing = await service.GetSharedMissingAchievementsAsync(
            "76561198000000001", "76561198000000002", 730);

        Assert.That(missing.Count, Is.EqualTo(0));
    }

    // ==================== GetSteamCuratorGames tests ====================

    [Test]
    public async Task GetSteamCuratorGamesAsync_ReturnsCuratorGames_WhenConfigured()
    {
        var handler = new FakeSteamHandler();
        handler.RegisterFromEmbedded("GetOwnedGames", "library-owned-games.json");

        var service = CreateService(handler);
        var games = (await service.GetSteamCuratorGamesAsync()).ToList();

        Assert.That(games.Count, Is.EqualTo(3));
    }

    [Test]
    public async Task GetSteamCuratorGamesAsync_ReturnsEmpty_WhenNotConfigured()
    {
        // Create service with empty curator ID and admin token
        var factory = new FakeHttpClientFactory(new FakeSteamHandler());
        var service = new SteamService(
            TestApiKey,
            "", // no admin token
            "", // no curator ID
            factory,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SteamService>.Instance);

        var games = (await service.GetSteamCuratorGamesAsync()).ToList();

        Assert.That(games.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task GetSteamCuratorGamesAsync_ReturnsEmpty_WhenApiFails()
    {
        var handler = new FakeSteamHandler();
        // No route — returns 404
        var service = CreateService(handler);
        var games = (await service.GetSteamCuratorGamesAsync()).ToList();

        Assert.That(games.Count, Is.EqualTo(0));
    }

    // ==================== Null-deserialization regression (locks in the 44b5bd0 guards) ====================
    // The API can answer HTTP 200 with a JSON `null` body. Unlike a malformed body (which throws
    // JsonException), a null body deserializes to a null POCO without throwing — exactly the case
    // 44b5bd0 guarded in GetSteamUserAsync/GetGameInfoAsync. These tests pin the safe-default
    // behaviour at every deserialize site in the same class: no NRE, no throw, sensible empty result.

    [Test]
    public async Task GetSteamUserAsync_ReturnsBlankUser_WhenApiReturnsNullBody()
    {
        var handler = new FakeSteamHandler();
        // 200 with JSON null — deserializes to a null SteamUserPOCO
        handler.Register("GetPlayerSummaries", 200, "null");

        var service = CreateService(handler);
        var user = await service.GetSteamUserAsync("76561198000000001");

        Assert.That(user, Is.Not.Null);
        Assert.That(user.SteamId, Is.EqualTo(null));
        Assert.That(user.SteamName, Is.EqualTo(null));
    }

    [Test]
    public async Task GetGameInfoAsync_ReturnsEmptyVM_WhenApiReturnsNullBody()
    {
        var handler = new FakeSteamHandler();
        // 200 with JSON null — deserializes to a null GameInfoPOCO
        handler.Register("appdetails", 200, "null");

        var service = CreateService(handler);
        var vm = await service.GetGameInfoAsync(new Game { AppId = 440 });

        Assert.That(vm, Is.Not.Null);
        Assert.That(vm._poco, Is.Null);
    }

    [Test]
    public async Task GetManyUsersAsync_ReturnsBlankUsersWithIds_WhenApiReturnsNullBody()
    {
        var handler = new FakeSteamHandler();
        handler.Register("GetPlayerSummaries", 200, "null");

        var service = CreateService(handler);
        var users = await service.GetManyUsersAsync(new List<string> { "76561198000000001", "76561198000000002" });

        Assert.That(users.Count, Is.EqualTo(2));
        // Requested ids are preserved so scoreboard alignment still works
        Assert.That(users[0].SteamId, Is.EqualTo("76561198000000001"));
        Assert.That(users[0].SteamName, Is.EqualTo(null));
        Assert.That(users[1].SteamId, Is.EqualTo("76561198000000002"));
        Assert.That(users[1].SteamName, Is.EqualTo(null));
    }

    // ==================== Malformed-body coverage for the previously-unguarded sites ====================
    // A 200 with a truncated/malformed body throws JsonException (not a null POCO). The
    // GetAchievementsAsync/GetSchemaAsync/GetGAPAsync trio already caught it; these tests pin the
    // same safe-default behaviour at the five sites that used to let the exception escape to
    // the controller layer: GetSteamUserAsync, GetManyUsersAsync, GetUserLevelAsync,
    // GetFriendsListAsync, and GetGamesGenericAsync (via GetGamesAsync).

    [Test]
    public async Task GetSteamUserAsync_ReturnsBlankUser_WhenApiReturnsMalformedJson()
    {
        var handler = new FakeSteamHandler();
        // Malformed JSON — missing closing brace
        handler.Register("GetPlayerSummaries", 200, "{\"response\":{\"players\":[{");

        var service = CreateService(handler);
        var user = await service.GetSteamUserAsync("76561198000000001");

        Assert.That(user, Is.Not.Null);
        Assert.That(user.SteamId, Is.EqualTo(null));
        Assert.That(user.SteamName, Is.EqualTo(null));
    }

    [Test]
    public async Task GetManyUsersAsync_ReturnsBlankUsersWithIds_WhenApiReturnsMalformedJson()
    {
        var handler = new FakeSteamHandler();
        // Malformed JSON — missing closing brace
        handler.Register("GetPlayerSummaries", 200, "{\"response\":{\"players\":[{");

        var service = CreateService(handler);
        var users = await service.GetManyUsersAsync(new List<string> { "76561198000000001", "76561198000000002" });

        Assert.That(users.Count, Is.EqualTo(2));
        // Requested ids are preserved so scoreboard alignment still works
        Assert.That(users[0].SteamId, Is.EqualTo("76561198000000001"));
        Assert.That(users[0].SteamName, Is.EqualTo(null));
        Assert.That(users[1].SteamId, Is.EqualTo("76561198000000002"));
        Assert.That(users[1].SteamName, Is.EqualTo(null));
    }

    [Test]
    public async Task GetUserLevelAsync_ReturnsZero_WhenApiReturnsMalformedJson()
    {
        var handler = new FakeSteamHandler();
        // Malformed JSON — missing closing brace
        handler.Register("GetSteamLevel", 200, "{\"response\":{\"player_level\":42");

        var service = CreateService(handler);
        var level = await service.GetUserLevelAsync("76561198000000001");

        Assert.That(level, Is.EqualTo(0));
    }

    [Test]
    public async Task GetFriendsListAsync_ReturnsEmpty_WhenApiReturnsMalformedJson()
    {
        var handler = new FakeSteamHandler();
        // Malformed JSON — missing closing brace
        handler.Register("GetFriendList", 200, "{\"friendslist\":{\"friends\":[{");

        var service = CreateService(handler);
        var friends = await service.GetFriendsListAsync("76561198000000001", 1);

        Assert.That(friends.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task GetFriendsListAsync_ReturnsEmpty_WhenFriendDetailsAreMalformedJson()
    {
        var handler = new FakeSteamHandler();
        // First call (friend list) succeeds; second call (player summaries) returns a malformed body
        handler.RegisterFromEmbedded("GetFriendList", "friends-list.json");
        handler.Register("GetPlayerSummaries", 200, "{\"response\":{\"players\":[{");

        var service = CreateService(handler);
        var friends = await service.GetFriendsListAsync("76561198000000001", 1);

        Assert.That(friends.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task GetGamesAsync_ReturnsEmpty_WhenApiReturnsMalformedJson()
    {
        var handler = new FakeSteamHandler();
        // Malformed JSON — missing closing brace
        handler.Register("GetOwnedGames", 200, "{\"response\":{\"games\":[{");

        var service = CreateService(handler);
        var games = (await service.GetGamesAsync("76561198000000001", 1)).ToList();

        Assert.That(games.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task GetUserLevelAsync_ReturnsZero_WhenApiReturnsNullBody()
    {
        var handler = new FakeSteamHandler();
        handler.Register("GetSteamLevel", 200, "null");

        var service = CreateService(handler);
        var level = await service.GetUserLevelAsync("76561198000000001");

        Assert.That(level, Is.EqualTo(0));
    }

    [Test]
    public async Task GetGamesAsync_ReturnsEmpty_WhenApiReturnsNullBody()
    {
        var handler = new FakeSteamHandler();
        handler.Register("GetOwnedGames", 200, "null");

        var service = CreateService(handler);
        var games = (await service.GetGamesAsync("76561198000000001", 1)).ToList();

        Assert.That(games.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task GetFriendsListAsync_ReturnsEmpty_WhenApiReturnsNullBody()
    {
        var handler = new FakeSteamHandler();
        handler.Register("GetFriendList", 200, "null");

        var service = CreateService(handler);
        var friends = await service.GetFriendsListAsync("76561198000000001", 1);

        Assert.That(friends.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task GetGameNewsAsync_ReturnsEmptyVM_WhenApiReturnsNullBody()
    {
        var handler = new FakeSteamHandler();
        handler.Register("GetNewsForApp", 200, "null");

        var service = CreateService(handler);
        var newsVM = await service.GetGameNewsAsync(new Game { AppId = 730 }, count: 5);

        Assert.That(newsVM, Is.Not.Null);
        Assert.That(newsVM._poco, Is.Null);
    }

    // ==================== Empty-body regression (200 with zero-length content) ====================
    // A CDN edge or reverse proxy can answer 200 with an empty body. That is valid as far as
    // IsSuccessStatusCode is concerned, but JsonSerializer.Deserialize throws on empty input.
    // GetAchievementsAsync/GetSchemaAsync/GetGAPAsync already catch JsonException and return null;
    // these tests pin that so the guard cannot be silently dropped.

    [Test]
    public async Task GetSteamUserAsync_ReturnsBlankUser_WhenApiReturnsEmptyBody()
    {
        var handler = new FakeSteamHandler();
        // 200 with a zero-length body
        handler.Register("GetPlayerSummaries", 200, "");

        var service = CreateService(handler);
        var user = await service.GetSteamUserAsync("76561198000000001");

        Assert.That(user, Is.Not.Null);
        Assert.That(user.SteamId, Is.EqualTo(null));
        Assert.That(user.SteamName, Is.EqualTo(null));
    }

    [Test]
    public async Task GetManyUsersAsync_ReturnsBlankUsersWithIds_WhenApiReturnsEmptyBody()
    {
        var handler = new FakeSteamHandler();
        handler.Register("GetPlayerSummaries", 200, "");

        var service = CreateService(handler);
        var users = await service.GetManyUsersAsync(new List<string> { "76561198000000001", "76561198000000002" });

        Assert.That(users.Count, Is.EqualTo(2));
        Assert.That(users[0].SteamId, Is.EqualTo("76561198000000001"));
        Assert.That(users[0].SteamName, Is.EqualTo(null));
        Assert.That(users[1].SteamId, Is.EqualTo("76561198000000002"));
        Assert.That(users[1].SteamName, Is.EqualTo(null));
    }

    [Test]
    public async Task GetUserLevelAsync_ReturnsZero_WhenApiReturnsEmptyBody()
    {
        var handler = new FakeSteamHandler();
        handler.Register("GetSteamLevel", 200, "");

        var service = CreateService(handler);
        var level = await service.GetUserLevelAsync("76561198000000001");

        Assert.That(level, Is.EqualTo(0));
    }

    [Test]
    public async Task GetGamesAsync_ReturnsEmpty_WhenApiReturnsEmptyBody()
    {
        var handler = new FakeSteamHandler();
        handler.Register("GetOwnedGames", 200, "");

        var service = CreateService(handler);
        var games = (await service.GetGamesAsync("76561198000000001", 1)).ToList();

        Assert.That(games.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task GetFriendsListAsync_ReturnsEmpty_WhenApiReturnsEmptyBody()
    {
        var handler = new FakeSteamHandler();
        handler.Register("GetFriendList", 200, "");

        var service = CreateService(handler);
        var friends = await service.GetFriendsListAsync("76561198000000001", 1);

        Assert.That(friends.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task GetGameInfoAsync_ReturnsEmptyVM_WhenApiReturnsEmptyBody()
    {
        var handler = new FakeSteamHandler();
        handler.Register("appdetails", 200, "");

        var service = CreateService(handler);
        var vm = await service.GetGameInfoAsync(new Game { AppId = 440 });

        Assert.That(vm, Is.Not.Null);
        Assert.That(vm._poco, Is.Null);
    }

    [Test]
    public async Task GetGameNewsAsync_ReturnsEmptyVM_WhenApiReturnsEmptyBody()
    {
        var handler = new FakeSteamHandler();
        handler.Register("GetNewsForApp", 200, "");

        var service = CreateService(handler);
        var newsVM = await service.GetGameNewsAsync(new Game { AppId = 730 }, count: 5);

        Assert.That(newsVM, Is.Not.Null);
        Assert.That(newsVM._poco, Is.Null);
    }

    [Test]
    public async Task GetAchievementsAsync_ReturnsNull_WhenApiReturnsEmptyBody()
    {
        var handler = new FakeSteamHandler();
        handler.Register("GetPlayerAchievements", 200, "");

        var service = CreateService(handler);
        var result = await service.GetAchievementsAsync("76561198000000001", 730);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetSchemaAsync_ReturnsNull_WhenApiReturnsEmptyBody()
    {
        var handler = new FakeSteamHandler();
        handler.Register("GetSchemaForGame", 200, "");

        var service = CreateService(handler);
        var result = await service.GetSchemaAsync(730);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetGAPAsync_ReturnsNull_WhenApiReturnsEmptyBody()
    {
        var handler = new FakeSteamHandler();
        handler.Register("GetGlobalAchievementPercentagesForApp", 200, "");

        var service = CreateService(handler);
        var result = await service.GetGAPAsync(730);

        Assert.That(result, Is.Null);
    }

    // ==================== Malformed-body coverage for the try/catch endpoints ====================
    // GetAchievementsAsync/GetSchemaAsync/GetGAPAsync are the only deserialize sites that rely on
    // the JsonException catch (the others fall through to the null-POCO guards above). Null-body
    // and empty-body are covered for the rest of the class; this pins the same three shapes here.

    [Test]
    public async Task GetAchievementsAsync_ReturnsNull_WhenApiReturnsNullBody()
    {
        var handler = new FakeSteamHandler();
        // 200 with JSON null — deserializes to a null AchievementRoot
        handler.Register("GetPlayerAchievements", 200, "null");

        var service = CreateService(handler);
        var result = await service.GetAchievementsAsync("76561198000000001", 730);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetSchemaAsync_ReturnsNull_WhenApiReturnsNullBody()
    {
        var handler = new FakeSteamHandler();
        handler.Register("GetSchemaForGame", 200, "null");

        var service = CreateService(handler);
        var result = await service.GetSchemaAsync(730);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetGAPAsync_ReturnsNull_WhenApiReturnsNullBody()
    {
        var handler = new FakeSteamHandler();
        handler.Register("GetGlobalAchievementPercentagesForApp", 200, "null");

        var service = CreateService(handler);
        var result = await service.GetGAPAsync(730);

        Assert.That(result, Is.Null);
    }
}
