using SteamProject.Models;

namespace SteamProject.DAL.Abstract;

public interface IBadgeRepository : IRepository<Badge>
{
    Task SaveChangesAsync();
}