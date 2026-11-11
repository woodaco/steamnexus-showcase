using Microsoft.EntityFrameworkCore;
using SendGrid.Helpers.Mail;
using SteamProject.DAL.Abstract;
using SteamProject.Data;
using SteamProject.Models;
using SteamProject.Models.DTO;

namespace SteamProject.DAL.Concrete;

public class UserRepository : Repository<User>, IUserRepository
{
    private readonly SteamInfoDbContext _ctx;

    public UserRepository(SteamInfoDbContext ctx) : base(ctx)
    {
        _ctx = ctx;
    }

    public User GetUser(string userId)
    {
        var currentUser = _ctx.Users
            .Include(u => u.UserBadges)
                .ThenInclude(ub => ub.Badge)
            .FirstOrDefault(u => u.AspNetUserId == userId);

        return currentUser;
    }

    public User GetUserWithAwardData(string userId)
    {
        return _ctx.Users
            .Include(u => u.UserBadges)
                .ThenInclude(ub => ub.Badge)
            .Include(u => u.Friends)
            .Include(u => u.UserGameInfos)
            .Include(u => u.UserAchievements)
            .FirstOrDefault(u => u.AspNetUserId == userId);
    }

    public Task<User?> GetUserWithAwardDataAsync(string userId)
    {
        return _ctx.Users
            .Include(u => u.UserBadges)
                .ThenInclude(ub => ub.Badge)
            .Include(u => u.Friends)
            .Include(u => u.UserGameInfos)
            .Include(u => u.UserAchievements)
            .FirstOrDefaultAsync(u => u.AspNetUserId == userId);
    }

    public void UpdateUserTheme(int userId, string theme)
    {
        var user = _ctx.Users.FirstOrDefault(u => u.Id == userId);
        if (user != null)
        {
            user.Theme = theme;
            _ctx.SaveChanges();
        }
    }

    public IEnumerable<User> GetAllUsers()
    {
        return GetAll().ToList();
    }

    public List<UserSummary> GetUsersBySteamId(IEnumerable<string> steamIds)
    {
        var ids = steamIds.Distinct().ToList();
        return GetAll(u => ids.Contains(u.SteamId))
            .Select(u => new UserSummary(u.Id, u.SteamId))
            .ToList();
    }
}