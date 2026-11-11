using SteamProject.Models;

namespace SteamProject.DAL.Abstract;

public interface IFriendRepository : IRepository<Friend>
{
    List<Friend> GetFriends(int userId);
    /// <summary>The caller's own friend row for the given SteamId (scoped by RootId so a SteamId shared
    /// across users resolves to THIS user's row, not another's).</summary>
    Friend GetSpecificFriend( string steamId, int rootId );
}