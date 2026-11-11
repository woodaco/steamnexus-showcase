using SteamProject.Models;
using SteamProject.Models.DTO;
using SteamProject.Helpers;
using SteamProject.ViewModels;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SteamProject.Services;

public class SteamService : ISteamService
{
    private readonly string _token;
    private readonly string _adminToken;
    private readonly string _curatorSteamId;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SteamService> _logger;

    public SteamService(
        string token,
        string adminToken,
        string curatorSteamId,
        IHttpClientFactory httpClientFactory,
        ILogger<SteamService> logger)
    {
        _token = token;
        _adminToken = adminToken;
        _curatorSteamId = curatorSteamId;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>True when the curator id + admin token the admin game-load needs are both configured.</summary>
    public bool IsCuratorConfigured =>
        !string.IsNullOrEmpty(_curatorSteamId) && !string.IsNullOrEmpty(_adminToken);

    #region Async Core Methods

    /// <summary>
    /// Async HTTP GET request to Steam API
    /// </summary>
    public async Task<string?> GetJsonStringFromEndpointAsync(string uri)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("Steam");
            var response = await client.GetAsync(uri);

            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(body))
                {
                    // An edge or proxy may answer 200 with a zero-length body. Treat it as a
                    // failed response: callers already degrade to safe defaults on null, while
                    // empty input would throw an unhandled ArgumentException in Deserialize.
                    _logger.LogWarning("API request to {Uri} returned an empty body", RedactSecrets(uri));
                    return null;
                }
                return body;
            }

            _logger.LogWarning("API request to {Uri} failed with status code {StatusCode}",
                RedactSecrets(uri), response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching data from endpoint: {Uri}", RedactSecrets(uri));
            return null;
        }
    }

    /// <summary>Masks the Steam API key (and any token query parameter) so it never reaches the logs.</summary>
    private static string RedactSecrets(string uri)
    {
        if (string.IsNullOrEmpty(uri)) return uri;
        return Regex.Replace(uri, @"(?i)((?:key|apikey|token)=)[^&\s]+", "$1***");
    }

    public async Task<User> GetSteamUserAsync(string steamid)
    {
        string uri = $"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/?key={_token}&steamids={steamid}";
        string? jsonResponse = await GetJsonStringFromEndpointAsync(uri);

        if (jsonResponse == null)
        {
            _logger.LogWarning("Failed to get Steam user data for SteamId: {SteamId}", steamid);
            return new User();
        }

        try
        {
            var poco = JsonSerializer.Deserialize<SteamUserPOCO>(jsonResponse);
            var returnMe = new User();
            if (poco != null)
            {
                returnMe.TakeSteamPOCO(poco);
            }
            return returnMe;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize Steam user data for SteamId: {SteamId}", steamid);
            return new User();
        }
    }

    public async Task<List<User>> GetManyUsersAsync(List<string> steamIds)
    {
        var userList = new List<User>();
        if (steamIds is null || steamIds.Count == 0)
            return userList;

        string joinedIds = string.Join(",", steamIds);
        string uri = $"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/?key={_token}&steamids={joinedIds}";

        string? jsonResponse = await GetJsonStringFromEndpointAsync(uri);

        if (jsonResponse == null)
        {
            _logger.LogWarning("Failed to get Steam user data for multiple users");
            // Return one blank-but-identified user per requested id so callers that align by SteamId still match.
            return MapManyUsers(null, steamIds);
        }

        try
        {
            var poco = JsonSerializer.Deserialize<SteamUserPOCO>(jsonResponse);
            return MapManyUsers(poco, steamIds);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize Steam user data for multiple users");
            return MapManyUsers(null, steamIds);
        }
    }

    /// <summary>
    /// Maps a GetPlayerSummaries response onto exactly one <see cref="User"/> per requested steamid,
    /// preserving request order. Steam does NOT preserve order and OMITS private/invalid steamids, so we
    /// key the returned players by steamid; a requested id with no returned player yields a blank user that
    /// still carries its SteamId (callers align scoreboards by SteamId). Pure + side-effect-free for testing.
    /// </summary>
    public static List<User> MapManyUsers(SteamUserPOCO? poco, List<string> steamIds)
    {
        var userList = new List<User>();
        if (steamIds is null || steamIds.Count == 0)
            return userList;

        var byId = poco?.response?.players?
            .Where(p => p?.steamid != null)
            .GroupBy(p => p.steamid)
            .ToDictionary(g => g.Key, g => g.First())
            ?? new Dictionary<string, Player>();

        foreach (var id in steamIds)
        {
            var userOut = new User();
            if (byId.TryGetValue(id, out var player))
                userOut.TakeSteamPlayer(player);
            else
                userOut.SteamId = id; // private/invalid profile: keep the id so score alignment still works
            userList.Add(userOut);
        }

        return userList;
    }

    /// <summary>
    /// Fills a blank SteamName/AvatarUrl on users returned by <see cref="MapManyUsers"/> (Steam API down,
    /// no key, or a private profile) from the locally stored copy of the same SteamId, so pages that list
    /// players never render a nameless card. Users that already have Steam data are left untouched.
    /// </summary>
    public static void FillMissingProfilesFromLocal(IEnumerable<User> steamUsers, IEnumerable<User> localUsers)
    {
        if (steamUsers is null || localUsers is null)
            return;

        var localById = localUsers
            .Where(u => !string.IsNullOrEmpty(u?.SteamId))
            .GroupBy(u => u.SteamId!)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var user in steamUsers)
        {
            if (user?.SteamId is null || !localById.TryGetValue(user.SteamId, out var local))
                continue;
            if (string.IsNullOrWhiteSpace(user.SteamName))
                user.SteamName = local.SteamName;
            if (string.IsNullOrWhiteSpace(user.AvatarUrl))
                user.AvatarUrl = local.AvatarUrl;
        }
    }

    public async Task<int> GetUserLevelAsync(string steamid)
    {
        string uri = $"https://api.steampowered.com/IPlayerService/GetSteamLevel/v1/?key={_token}&steamid={steamid}";
        string? jsonResponse = await GetJsonStringFromEndpointAsync(uri);

        if (jsonResponse == null)
        {
            _logger.LogWarning("Failed to get Steam level for SteamId: {SteamId}", steamid);
            return 0;
        }

        try
        {
            var result = JsonSerializer.Deserialize<UserLevelPOCO>(jsonResponse);
            return result?.response?.player_level ?? 0;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize Steam level for SteamId: {SteamId}", steamid);
            return 0;
        }
    }

    public async Task<List<Friend>> GetFriendsListAsync(string steamid, int userId)
    {
        string friendsListUri = $"https://api.steampowered.com/ISteamUser/GetFriendList/v1/?key={_token}&steamid={steamid}";
        string? jsonResponse = await GetJsonStringFromEndpointAsync(friendsListUri);

        if (jsonResponse == null)
        {
            _logger.LogWarning("Failed to get friends list for SteamId: {SteamId}", steamid);
            return new List<Friend>();
        }

        FriendsListPOCO? friendPoco;
        try
        {
            friendPoco = JsonSerializer.Deserialize<FriendsListPOCO>(jsonResponse);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize friends list for SteamId: {SteamId}", steamid);
            return new List<Friend>();
        }
        var friendPocoList = friendPoco?.friendslist?.friends;
        if (friendPocoList == null)
        {
            return new List<Friend>();
        }

        var idList = friendPocoList.Select(f => f.steamid).ToList();
        string friendIdsParam = string.Join(",", idList);

        string friendsInfoUri = $"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/?key={_token}&steamids={friendIdsParam}";
        jsonResponse = await GetJsonStringFromEndpointAsync(friendsInfoUri);

        if (jsonResponse == null)
        {
            return new List<Friend>();
        }

        SteamUserPOCO? friendsPoco;
        try
        {
            friendsPoco = JsonSerializer.Deserialize<SteamUserPOCO>(jsonResponse);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize friend details for SteamId: {SteamId}", steamid);
            return new List<Friend>();
        }
        var friendsData = friendsPoco?.response?.players;
        if (friendsData == null)
        {
            return new List<Friend>();
        }

        var friendsList = new List<Friend>();
        foreach (var friend in friendsData)
        {
            var friendOut = new Friend();
            friendOut.TakePlayerPOCO(friend);
            friendOut.RootId = userId;
            friendsList.Add(friendOut);
        }

        return friendsList.OrderBy(f => f.SteamName).ToList();
    }

    public async Task<Friend?> GetFriendSpecificAsync(string userSteamId, int userId, string friendSteamId)
    {
        var friends = await GetFriendsListAsync(userSteamId, userId);
        return friends.FirstOrDefault(f => f.SteamId == friendSteamId);
    }

    public async Task<IEnumerable<Game>> GetGamesAsync(string userSteamId, int userId)
    {
        string source = $"http://api.steampowered.com/IPlayerService/GetOwnedGames/v0001/?key={_token}&steamid={userSteamId}&format=json&include_appinfo=1";
        return await GetGamesGenericAsync(source, userId);
    }

    public async Task<IEnumerable<Game>> GetSteamCuratorGamesAsync()
    {
        if (string.IsNullOrEmpty(_curatorSteamId))
        {
            _logger.LogWarning("Curator Steam ID not configured");
            return Enumerable.Empty<Game>();
        }

        string source = $"http://api.steampowered.com/IPlayerService/GetOwnedGames/v0001/?key={_adminToken}&steamid={_curatorSteamId}&format=json&include_appinfo=1";
        return await GetGamesGenericAsync(source, 1);
    }

    public async Task<IEnumerable<Game>> GetGamesGenericAsync(string source, int userId)
    {
        string? jsonResponse = await GetJsonStringFromEndpointAsync(source);

        if (jsonResponse == null)
        {
            _logger.LogWarning("Failed to get games from source: {Source}", RedactSecrets(source));
            return Enumerable.Empty<Game>();
        }

        LibraryPOCO? poco;
        try
        {
            poco = JsonSerializer.Deserialize<LibraryPOCO>(jsonResponse);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize games from source: {Source}", RedactSecrets(source));
            return Enumerable.Empty<Game>();
        }
        var games = new List<Game>();

        if (poco?.response?.games is not null && poco.response.games.Any())
        {
            foreach (var game in poco.response.games)
            {
                var temp = new Game();
                temp = temp.TakeLibraryInfoPOCO(game, userId);
                games.Add(temp);
            }
        }

        return games.OrderBy(g => g.Name);
    }

    public async Task<IEnumerable<Game>> GetSharedGamesAsync(string userSteamId, string friendSteamId, int userId)
    {
        // Fetch both game lists in parallel for better performance
        var myGamesTask = GetGamesAsync(userSteamId, userId);
        var friendsGamesTask = GetGamesAsync(friendSteamId, 0);

        await Task.WhenAll(myGamesTask, friendsGamesTask);

        var myGames = (await myGamesTask).ToList();
        var friendsGames = (await friendsGamesTask).ToList();
        var sharedGames = myGames.Join(friendsGames, g1 => g1.AppId, g2 => g2.AppId, (g1, g2) => g1).ToList();

        return sharedGames.OrderBy(g => g.Name);
    }

    public async Task<GameVM> GetGameInfoAsync(Game game)
    {
        var gameVM = new GameVM();
        string source = $"https://store.steampowered.com/api/appdetails?appids={game.AppId}&l=en";
        string? jsonResponse = await GetJsonStringFromEndpointAsync(source);

        if (jsonResponse == null)
        {
            _logger.LogWarning("Failed to get game info for AppId: {AppId}", game.AppId);
            return gameVM;
        }

        var regex = new Regex(Regex.Escape(game.AppId.ToString()));
        jsonResponse = regex.Replace(jsonResponse, "response", 1);

        jsonResponse = TryReplaceRequirements(jsonResponse, "linux_requirements");
        jsonResponse = TryReplaceRequirements(jsonResponse, "mac_requirements");
        jsonResponse = TryReplaceRequirements(jsonResponse, "pc_requirements");

        try
        {
            var poco = JsonSerializer.Deserialize<GameInfoPOCO>(jsonResponse);
            if (poco != null)
            {
                gameVM._poco = poco;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize game info for AppId: {AppId}", game.AppId);
        }

        return gameVM;
    }

    public async Task<GameNewsVM> GetGameNewsAsync(Game game, int count = 10)
    {
        var gameVM = new GameNewsVM();
        int fetchCount = 2 + count * 2;
        string source = $"https://api.steampowered.com/ISteamNews/GetNewsForApp/v0002/?appid={game.AppId}&count={fetchCount}&l=en";
        string? jsonResponse = await GetJsonStringFromEndpointAsync(source);

        if (jsonResponse == null)
        {
            _logger.LogWarning("Failed to get news for AppId: {AppId}", game.AppId);
            return gameVM;
        }

        GameNewsPoco? newsPoco;
        try
        {
            newsPoco = JsonSerializer.Deserialize<GameNewsPoco>(jsonResponse);
        }
        catch (JsonException)
        {
            _logger.LogWarning("Failed to deserialize news for AppId: {AppId}", game.AppId);
            return gameVM;
        }
        var filteredNewsItems = new List<Newsitem>();

        if (newsPoco?.appnews?.newsitems != null)
        {
            for (var i = 0; i < newsPoco.appnews.newsitems.Count && filteredNewsItems.Count < count; i++)
            {
                var newsItem = newsPoco.appnews.newsitems[i];

                if (!string.Equals(newsItem.feedlabel, "SteamDb", StringComparison.OrdinalIgnoreCase))
                {
                    newsItem.contents = HelperMethods.StripJunkFromString(newsItem.contents);
                    newsItem.dateTime = HelperMethods.UnixTimeStampToDateTime(newsItem.date);
                    filteredNewsItems.Add(newsItem);
                }
            }

            newsPoco.appnews.newsitems = filteredNewsItems.Take(count).ToList();
            gameVM._poco = newsPoco;
        }

        return gameVM;
    }

    public async Task<AchievementRoot?> GetAchievementsAsync(string userSteamId, int appId)
    {
        string source = $"http://api.steampowered.com/ISteamUserStats/GetPlayerAchievements/v1/?appid={appId}&key={_token}&steamid={userSteamId}&l=en";
        string? response = await GetJsonStringFromEndpointAsync(source);

        if (response == null)
        {
            _logger.LogDebug("No achievements found for AppId: {AppId}, SteamId: {SteamId}", appId, userSteamId);
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AchievementRoot>(response);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize achievements for AppId: {AppId}", appId);
            return null;
        }
    }

    public async Task<List<Achievement>> GetSharedMissingAchievementsAsync(string userSteamId, string friendSteamId, int appId)
    {
        // Fetch both achievement lists in parallel
        var userAchievementsTask = GetAchievementsAsync(userSteamId, appId);
        var friendAchievementsTask = GetAchievementsAsync(friendSteamId, appId);

        await Task.WhenAll(userAchievementsTask, friendAchievementsTask);

        var userAchievementResult = await userAchievementsTask;
        var friendAchievementResult = await friendAchievementsTask;

        var userAchList = userAchievementResult?.playerstats?.achievements ?? new List<Achievement>();
        var friendAchList = friendAchievementResult?.playerstats?.achievements ?? new List<Achievement>();

        var sharedMissingAchievements = new List<Achievement>();

        foreach (var userAch in userAchList)
        {
            foreach (var friendAch in friendAchList)
            {
                if (userAch.apiname == friendAch.apiname &&
                    userAch.achieved == friendAch.achieved &&
                    userAch.achieved == 0)
                {
                    sharedMissingAchievements.Add(userAch);
                }
            }
        }

        return sharedMissingAchievements.OrderBy(ach => ach.name).ToList();
    }

    public async Task<SchemaRoot?> GetSchemaAsync(int appId)
    {
        string source = $"https://api.steampowered.com/ISteamUserStats/GetSchemaForGame/v2/?appid={appId}&key={_token}&l=en";
        string? response = await GetJsonStringFromEndpointAsync(source);

        if (response == null)
        {
            _logger.LogDebug("No schema found for AppId: {AppId}", appId);
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SchemaRoot>(response);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize schema for AppId: {AppId}", appId);
            return null;
        }
    }

    public async Task<GAPRoot?> GetGAPAsync(int appId)
    {
        string source = $"https://api.steampowered.com/ISteamUserStats/GetGlobalAchievementPercentagesForApp/v0002/?gameid={appId}&format=json";
        string? response = await GetJsonStringFromEndpointAsync(source);

        if (response == null)
        {
            _logger.LogDebug("No global achievement percentages found for AppId: {AppId}", appId);
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<GAPRoot>(response);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize GAP for AppId: {AppId}", appId);
            return null;
        }
    }

    #endregion

    #region Helper Methods

    private string TryReplaceRequirements(string json, string requirementType)
    {
        try
        {
            var regex = new Regex(Regex.Escape($"{requirementType}\":") + @"\s*\[\]");
            return regex.Replace(json, $"{requirementType}\":{{}}", 1);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not replace {RequirementType} in json", requirementType);
            return json;
        }
    }

    public HashSet<string> ExtractGenresFromGameInfo(GameVM gameVM)
    {
        var genres = new HashSet<string>();
        if (gameVM?._poco?.response?.data?.genres == null)
            return genres;

        foreach (var genre in gameVM._poco.response.data.genres)
        {
            if (!string.IsNullOrWhiteSpace(genre.description))
            {
                // Clean up any stray quotes or whitespace
                var cleanGenre = genre.description.Trim().Trim('"', '\'');
                if (!string.IsNullOrWhiteSpace(cleanGenre))
                    genres.Add(cleanGenre);
            }
        }
        return genres;
    }

    public HashSet<string> ExtractCategoriesFromGameInfo(GameVM gameVM)
    {
        var categories = new HashSet<string>();
        if (gameVM?._poco?.response?.data?.categories == null)
            return categories;

        foreach (var cat in gameVM._poco.response.data.categories)
        {
            if (!string.IsNullOrWhiteSpace(cat.description))
            {
                // Clean up any stray quotes or whitespace
                var cleanCategory = cat.description.Trim().Trim('"', '\'');
                if (!string.IsNullOrWhiteSpace(cleanCategory))
                    categories.Add(cleanCategory);
            }
        }
        return categories;
    }

    #endregion
}
