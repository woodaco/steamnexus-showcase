using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using SteamProject.Models;
using SteamProject.Services;

namespace NUnit_Tests.ServiceTesting
{
    /// <summary>
    /// Proves the just-in-time domain-User provisioning: a missing row is created exactly once, an
    /// existing row is never duplicated, and a null/empty id is a no-op. This is the fix for the
    /// Steam-first external-login gap (IdentityUser created, domain row missing -> NRE everywhere).
    /// </summary>
    public class UserProvisioningServiceTests
    {
        private static SteamInfoDbContext NewCtx(string dbName) =>
            new SteamInfoDbContext(new DbContextOptionsBuilder<SteamInfoDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options);

        private static UserProvisioningService Make(SteamInfoDbContext ctx) =>
            new UserProvisioningService(ctx, new MemoryCache(new MemoryCacheOptions()),
                Mock.Of<ILogger<UserProvisioningService>>());

        [Test]
        public async Task EnsureDomainUser_CreatesTheRow_WhenMissing()
        {
            using var ctx = NewCtx(nameof(EnsureDomainUser_CreatesTheRow_WhenMissing));
            var svc = Make(ctx);

            await svc.EnsureDomainUserAsync("aspnet-new-user");

            Assert.That(ctx.Users.Count(u => u.AspNetUserId == "aspnet-new-user"), Is.EqualTo(1));
        }

        [Test]
        public async Task EnsureDomainUser_DoesNotDuplicate_WhenAlreadyPresent()
        {
            using var ctx = NewCtx(nameof(EnsureDomainUser_DoesNotDuplicate_WhenAlreadyPresent));
            ctx.Users.Add(new User { AspNetUserId = "aspnet-existing" });
            ctx.SaveChanges();
            var svc = Make(ctx);

            await svc.EnsureDomainUserAsync("aspnet-existing");

            Assert.That(ctx.Users.Count(u => u.AspNetUserId == "aspnet-existing"), Is.EqualTo(1));
        }

        [Test]
        public async Task EnsureDomainUser_NullOrEmpty_IsANoOp()
        {
            using var ctx = NewCtx(nameof(EnsureDomainUser_NullOrEmpty_IsANoOp));
            var svc = Make(ctx);

            await svc.EnsureDomainUserAsync("");

            Assert.That(ctx.Users.Count(), Is.EqualTo(0));
        }
    }
}
