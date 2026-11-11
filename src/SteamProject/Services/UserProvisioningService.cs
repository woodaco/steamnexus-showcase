using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SteamProject.Models;

namespace SteamProject.Services
{
    /// <summary>
    /// Guarantees that every authenticated identity has a paired domain <see cref="User"/> row.
    /// Registration creates this row, but an external (Steam-first) login goes through the Default
    /// Identity UI, which only creates the IdentityUser — leaving the domain row missing and NRE'ing
    /// Library/Friends/Compete. This provisions the row just-in-time for ANY auth path. Idempotent and
    /// cached, so the steady-state cost is a memory lookup (no DB hit) once a user is known-provisioned.
    /// </summary>
    public interface IUserProvisioningService
    {
        Task EnsureDomainUserAsync(string aspNetUserId);
    }

    public class UserProvisioningService : IUserProvisioningService
    {
        private readonly SteamInfoDbContext _ctx;
        private readonly IMemoryCache _cache;
        private readonly ILogger<UserProvisioningService> _logger;

        public UserProvisioningService(SteamInfoDbContext ctx, IMemoryCache cache, ILogger<UserProvisioningService> logger)
        {
            _ctx = ctx;
            _cache = cache;
            _logger = logger;
        }

        public async Task EnsureDomainUserAsync(string aspNetUserId)
        {
            if (string.IsNullOrEmpty(aspNetUserId)) return;

            var cacheKey = $"domain-user-provisioned:{aspNetUserId}";
            if (_cache.TryGetValue(cacheKey, out _)) return; // already known to exist

            var exists = await _ctx.Users.AnyAsync(u => u.AspNetUserId == aspNetUserId);
            if (!exists)
            {
                _ctx.Users.Add(new User { AspNetUserId = aspNetUserId });
                try
                {
                    await _ctx.SaveChangesAsync();
                    _logger.LogInformation(
                        "Provisioned a missing domain User row for an authenticated identity (e.g. a Steam-first external login).");
                }
                catch (DbUpdateException)
                {
                    // A concurrent request already provisioned it (unique index on AspNetUserId); fine.
                }
            }

            _cache.Set(cacheKey, true, TimeSpan.FromHours(1));
        }
    }
}
