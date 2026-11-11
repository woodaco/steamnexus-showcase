using SteamProject.Services;
using SteamProject.Models;
using SteamProject.Models.DTO;
using SteamProject.ViewModels;

namespace SteamProject.Demo;

/// <summary>
/// Wraps the real <see cref="ISteamService"/> so a demo session never reaches the Steam Web API.
/// Outside a demo session every call goes straight to the inner service. Inside one, profile
/// lookups are answered from the seeded database rows, list lookups come back empty and the
/// remaining calls throw (the pages that make them are already 403 for a demo session, so a throw
/// here only fires if that allowlist is ever widened by mistake).
/// </summary>
public sealed class DemoGuardSteamService : ISteamService
{
    private readonly ISteamService _inner;
    private readonly IHttpContextAccessor _accessor;
    private readonly IServiceProvider _services;

    public DemoGuardSteamService(ISteamService inner, IHttpContextAccessor accessor, IServiceProvider services)
    {
        _inner = inner;
        _accessor = accessor;
        _services = services;
    }

    private bool Demo => DemoMode.IsDemoRequest(_accessor);

    private List<User> LocalUsers(IEnumerable<string> steamIds)
    {
        var ids = steamIds.Where(s => !string.IsNullOrEmpty(s)).ToHashSet();
        var ctx = _services.GetRequiredService<SteamInfoDbContext>();
        var local = ctx.Users.Where(u => u.SteamId != null && ids.Contains(u.SteamId)).ToList();
        // Detached copies: callers mutate these (scores, names) and must not write them back.
        return ids.Select(id =>
        {
            var match = local.FirstOrDefault(u => u.SteamId == id);
            return new User
            {
                AspNetUserId = match?.AspNetUserId ?? string.Empty,
                SteamId = id,
                SteamName = match?.SteamName,
                AvatarUrl = match?.AvatarUrl,
                PlayerLevel = match?.PlayerLevel,
            };
        }).ToList();
    }

    private static InvalidOperationException Blocked(string call) =>
        new($"{call} is not available to the demo persona; the demo never calls the Steam Web API.");

    public bool IsCuratorConfigured => !Demo && _inner.IsCuratorConfigured;

    public Task<User> GetSteamUserAsync(string steamid) =>
        Demo ? Task.FromResult(LocalUsers(new[] { steamid }).First()) : _inner.GetSteamUserAsync(steamid);

    public Task<List<User>> GetManyUsersAsync(List<string> steamIds) =>
        Demo ? Task.FromResult(LocalUsers(steamIds ?? new List<string>())) : _inner.GetManyUsersAsync(steamIds!);

    public Task<int> GetUserLevelAsync(string steamid) =>
        Demo ? Task.FromResult(LocalUsers(new[] { steamid }).First().PlayerLevel ?? 0) : _inner.GetUserLevelAsync(steamid);

    public Task<List<Friend>> GetFriendsListAsync(string steamid, int userId) =>
        Demo ? Task.FromResult(new List<Friend>()) : _inner.GetFriendsListAsync(steamid, userId);

    public Task<Friend?> GetFriendSpecificAsync(string userSteamId, int userId, string friendSteamId) =>
        Demo ? Task.FromResult<Friend?>(null) : _inner.GetFriendSpecificAsync(userSteamId, userId, friendSteamId);

    public Task<IEnumerable<Game>> GetGamesAsync(string userSteamId, int userId) =>
        Demo ? Task.FromResult(Enumerable.Empty<Game>()) : _inner.GetGamesAsync(userSteamId, userId);

    public Task<IEnumerable<Game>> GetSharedGamesAsync(string userSteamId, string friendSteamId, int userId) =>
        Demo ? Task.FromResult(Enumerable.Empty<Game>()) : _inner.GetSharedGamesAsync(userSteamId, friendSteamId, userId);

    public Task<IEnumerable<Game>> GetGamesGenericAsync(string source, int userId) =>
        Demo ? Task.FromResult(Enumerable.Empty<Game>()) : _inner.GetGamesGenericAsync(source, userId);

    public Task<IEnumerable<Game>> GetSteamCuratorGamesAsync() =>
        Demo ? Task.FromResult(Enumerable.Empty<Game>()) : _inner.GetSteamCuratorGamesAsync();

    public Task<GameNewsVM> GetGameNewsAsync(Game game, int count = 10) =>
        Demo ? throw Blocked(nameof(GetGameNewsAsync)) : _inner.GetGameNewsAsync(game, count);

    public Task<GameVM> GetGameInfoAsync(Game game) =>
        Demo ? throw Blocked(nameof(GetGameInfoAsync)) : _inner.GetGameInfoAsync(game);

    public HashSet<string> ExtractGenresFromGameInfo(GameVM gameVM) => _inner.ExtractGenresFromGameInfo(gameVM);

    public HashSet<string> ExtractCategoriesFromGameInfo(GameVM gameVM) => _inner.ExtractCategoriesFromGameInfo(gameVM);

    public Task<AchievementRoot?> GetAchievementsAsync(string userSteamId, int appId) =>
        Demo ? Task.FromResult<AchievementRoot?>(null) : _inner.GetAchievementsAsync(userSteamId, appId);

    public Task<List<Achievement>> GetSharedMissingAchievementsAsync(string userSteamId, string friendSteamId, int appId) =>
        Demo ? Task.FromResult(new List<Achievement>()) : _inner.GetSharedMissingAchievementsAsync(userSteamId, friendSteamId, appId);

    public Task<SchemaRoot?> GetSchemaAsync(int appId) =>
        Demo ? Task.FromResult<SchemaRoot?>(null) : _inner.GetSchemaAsync(appId);

    public Task<GAPRoot?> GetGAPAsync(int appId) =>
        Demo ? Task.FromResult<GAPRoot?>(null) : _inner.GetGAPAsync(appId);
}

/// <summary>Wraps the OpenAI service so a demo session never sends a summarisation request.</summary>
public sealed class DemoGuardOpenAiService : IOpenAiApiService
{
    private readonly IOpenAiApiService _inner;
    private readonly IHttpContextAccessor _accessor;

    public DemoGuardOpenAiService(IOpenAiApiService inner, IHttpContextAccessor accessor)
    {
        _inner = inner;
        _accessor = accessor;
    }

    private const string Disabled = "News summaries are turned off for the demo persona.";

    public Task<string> SummarizeNewsShortAsync(string inputString) =>
        DemoMode.IsDemoRequest(_accessor) ? Task.FromResult(Disabled) : _inner.SummarizeNewsShortAsync(inputString);

    public Task<string> SummarizeNewsLongAsync(string inputString) =>
        DemoMode.IsDemoRequest(_accessor) ? Task.FromResult(Disabled) : _inner.SummarizeNewsLongAsync(inputString);
}
