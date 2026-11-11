using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SteamProject.DAL.Abstract;
using SteamProject.Models;
using SteamProject.Models.DTO;

namespace SteamProject.DAL.Concrete;


public class BadgeRepository : Repository<Badge>,  IBadgeRepository
{
    private readonly SteamInfoDbContext _context;

    public BadgeRepository(SteamInfoDbContext ctx) : base(ctx)
    {
        _context = ctx;
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

}
