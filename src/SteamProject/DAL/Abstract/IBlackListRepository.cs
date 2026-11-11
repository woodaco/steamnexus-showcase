using SteamProject.Models;

namespace SteamProject.DAL.Abstract;

public interface IBlackListRepository : IRepository<BlackList>
{
    IEnumerable<string> GetBlackList();
    bool CheckForBlackList(string id);
    /// <summary>Removes every blacklist entry for the given SteamId (idempotent). Returns true if any were removed.</summary>
    bool RemoveFromBlackList(string steamId);
}