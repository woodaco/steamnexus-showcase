using SteamProject.DAL.Abstract;
using SteamProject.Data;
using SteamProject.Models;

namespace SteamProject.DAL.Concrete;

public class BlackListRepository : Repository<BlackList>, IBlackListRepository
{
    public BlackListRepository(SteamInfoDbContext ctx) : base(ctx)
    {
    }
    public IEnumerable<string> GetBlackList()
    {
        // Filter out null SteamIds so the list never surfaces blank/garbage entries.
        return GetAll().Where(b => b.SteamId != null).Select(b => b.SteamId).ToList();
    }

    public bool CheckForBlackList(string id)
    {
        // DB-side existence check instead of loading the whole table into memory per Steam login.
        return GetAll().Any(b => b.SteamId == id);
    }

    public bool RemoveFromBlackList(string steamId)
    {
        var rows = GetAll().Where(b => b.SteamId == steamId).ToList();
        if (rows.Count == 0) return false;
        foreach (var row in rows)
            Delete(row);
        return true;
    }
}