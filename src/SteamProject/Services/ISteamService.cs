using SteamProject.Models;
using SteamProject.Models.DTO;
using SteamProject.ViewModels;

namespace SteamProject.Services;

public interface ISteamService
{
    /// <summary>True when the curator Steam id + admin token needed for the admin game-load are configured.</summary>
    bool IsCuratorConfigured { get; }

    // Async methods (preferred)
    Task<User> GetSteamUserAsync(string steamid);
    Task<List<User>> GetManyUsersAsync(List<string> steamIds);
    Task<int> GetUserLevelAsync(string steamid);
    Task<List<Friend>> GetFriendsListAsync(string steamid, int userId);
    Task<Friend?> GetFriendSpecificAsync(string userSteamId, int userId, string friendSteamId);
    Task<IEnumerable<Game>> GetGamesAsync(string userSteamId, int userId);
    Task<IEnumerable<Game>> GetSharedGamesAsync(string userSteamId, string friendSteamId, int userId);
    Task<IEnumerable<Game>> GetGamesGenericAsync(string source, int userId);
    Task<IEnumerable<Game>> GetSteamCuratorGamesAsync();
    Task<GameNewsVM> GetGameNewsAsync(Game game, int count = 10);
    Task<GameVM> GetGameInfoAsync(Game game);
    HashSet<string> ExtractGenresFromGameInfo(GameVM gameVM);
    HashSet<string> ExtractCategoriesFromGameInfo(GameVM gameVM);
    Task<AchievementRoot?> GetAchievementsAsync(string userSteamId, int appId);
    Task<List<Achievement>> GetSharedMissingAchievementsAsync(string userSteamId, string friendSteamId, int appId);
    Task<SchemaRoot?> GetSchemaAsync(int appId);
    Task<GAPRoot?> GetGAPAsync(int appId);
}
