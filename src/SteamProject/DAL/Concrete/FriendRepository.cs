using SteamProject.DAL.Abstract;
using SteamProject.Models;

namespace SteamProject.DAL.Concrete;

public class FriendRepository : Repository<Friend>, IFriendRepository
{
    public FriendRepository(SteamInfoDbContext ctx) : base(ctx)
    {

    }

    public List<Friend> GetFriends(int userId)
    {
        return GetAll().Where(x => x.RootId == userId ).ToList<Friend>();
    }

    public Friend GetSpecificFriend(string steamId, int rootId)
    {
        // Scope by RootId: a SteamId can appear as a friend of multiple users, so filtering by SteamId
        // alone could return another user's row (and then the caller's ownership check wrongly 404s).
        return GetAll().Where( f => f.SteamId == steamId && f.RootId == rootId ).FirstOrDefault();
    }
    
}