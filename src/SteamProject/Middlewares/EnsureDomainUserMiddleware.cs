using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SteamProject.Services;

namespace SteamProject.Middlewares
{
    /// <summary>
    /// For every authenticated request, ensures the caller has a paired domain User row before the
    /// controllers run (they resolve and dereference it). Closes the Steam-first external-login gap
    /// where the Default Identity UI creates an IdentityUser but no domain row.
    /// </summary>
    public class EnsureDomainUserMiddleware
    {
        private readonly RequestDelegate _next;

        public EnsureDomainUserMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IServiceScopeFactory scopeFactory)
        {
            if (context.User?.Identity?.IsAuthenticated == true)
            {
                using var scope = scopeFactory.CreateScope();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
                var provisioning = scope.ServiceProvider.GetRequiredService<IUserProvisioningService>();

                var aspNetUserId = userManager.GetUserId(context.User);
                if (aspNetUserId is not null)
                    await provisioning.EnsureDomainUserAsync(aspNetUserId);
            }

            await _next(context);
        }
    }
}
