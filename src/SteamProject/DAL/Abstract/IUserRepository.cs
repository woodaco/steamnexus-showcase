using SteamProject.Models;
using SteamProject.Models.DTO;

namespace SteamProject.DAL.Abstract
{

    public interface IUserRepository : IRepository<User>
    {
        /// <summary>
        /// Get information about the current user.
        /// </summary>
        /// <param name="userId">The identity ID of the current user.</param>
        /// <returns>A new User with all of the info in the User table about them</returns>
        User GetUser(string userId);

        /// <summary>
        /// Loads a user with the collections the award conditions evaluate (Friends,
        /// UserGameInfos, UserAchievements, UserBadges) eager-loaded, to avoid an N+1 lazy-load
        /// storm on every badge-check poll.
        /// </summary>
        User GetUserWithAwardData(string userId);

        /// <summary>
        /// Async counterpart of <see cref="GetUserWithAwardData"/> for use inside async handlers.
        /// </summary>
        Task<User?> GetUserWithAwardDataAsync(string userId);

        /// <summary>
        /// Get a list of all users on the site -- used solely for admin access right now.
        /// <summary>
        /// <returns>A list of all the users on the site.
        IEnumerable<User> GetAllUsers();

        /// <summary>
        /// Resolve a minimal (Id, SteamId) summary for each user whose SteamId is in
        /// <paramref name="steamIds"/>, pushing that filter down into the query so only
        /// the matching rows (not the whole users table) are transferred and proxied.
        /// Returns only the requested ids; ids with no registered user are absent.
        /// </summary>
        List<UserSummary> GetUsersBySteamId(IEnumerable<string> steamIds);

        void UpdateUserTheme(int userId, string theme);
    }
}