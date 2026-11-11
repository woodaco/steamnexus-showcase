using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using AspNet.Security.OpenId;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Polly;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SteamProject.Services;
using SteamProject.Models;
using SteamProject.DAL.Abstract;
using SteamProject.DAL.Concrete;
using Microsoft.AspNetCore.Identity.UI.Services;
using SteamProject.Areas.Identity.Data;
using OpenAI.Extensions;
using SteamProject.Data;
using SteamProject.Utilities;
using System.Reflection;
using SteamProject.Models.Awards.Abstract;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Database connection configuration
// In Development: Uses local connection strings from appsettings.json
// In Production: Uses connection strings from Azure App Configuration or environment variables
var authConnectionString = builder.Configuration.GetConnectionString("SteamInfoAuthConnection")
    ?? throw new InvalidOperationException("Connection string 'SteamInfoAuthConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseLazyLoadingProxies().UseSqlServer(authConnectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
        sqlOptions.CommandTimeout(60);
    }));

var steamInfoConnectionString = builder.Configuration.GetConnectionString("SteamInfoConnection")
    ?? throw new InvalidOperationException("Connection string 'SteamInfoConnection' not found.");
builder.Services.AddDbContext<SteamInfoDbContext>(options =>
    options.UseLazyLoadingProxies().UseSqlServer(steamInfoConnectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
        sqlOptions.CommandTimeout(60);
    }));


// API Keys and tokens from configuration
var steamApiToken = builder.Configuration["SteamKey"] ?? string.Empty;
var openAiToken = builder.Configuration["OpenAiKey"] ?? string.Empty;
var adminSeedingApiToken = builder.Configuration["AdminSeedingApiKey"] ?? string.Empty;
var curatorSteamId = builder.Configuration["Steam:CuratorSteamId"] ?? string.Empty;

// Register HttpClient factory with named clients
// Steam Web API client with retry/backoff for transient failures, including HTTP 429 (rate limit).
builder.Services.AddHttpClient("Steam")
    .AddTransientHttpErrorPolicy(policy => policy
        .OrResult(response => (int)response.StatusCode == 429)
        .WaitAndRetryAsync(3, attempt => TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt))));

builder.Services.AddHttpContextAccessor();
// DemoGuardSteamService passes every call through, except for a read-only demo session, which is
// answered from the database so the demo never reaches the Steam Web API.
builder.Services.AddScoped<ISteamService>(sp => new SteamProject.Demo.DemoGuardSteamService(
    new SteamService(
        steamApiToken,
        adminSeedingApiToken,
        curatorSteamId,
        sp.GetRequiredService<IHttpClientFactory>(),
        sp.GetRequiredService<ILogger<SteamService>>()),
    sp.GetRequiredService<IHttpContextAccessor>(),
    sp));
// Cache OpenAI news summaries server-side (decorator), keyed by content hash, to cut cost + latency.
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IOpenAiApiService>(sp => new SteamProject.Demo.DemoGuardOpenAiService(
    new CachingOpenAiApiService(
        new OpenAiApiService(openAiToken),
        sp.GetRequiredService<IMemoryCache>(),
        sp.GetRequiredService<ILogger<CachingOpenAiApiService>>()),
    sp.GetRequiredService<IHttpContextAccessor>()));
// Per-user rate limit for the (paid) OpenAI-backed news summaries so a single user can't run up cost.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("news-summary", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User?.Identity?.Name
                ?? httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 15,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserGameInfoRepository, UserGameInfoRepository>();
builder.Services.AddScoped<IGameRepository, GameRepository>();
builder.Services.AddScoped<IFriendRepository, FriendRepository>();
builder.Services.AddScoped<IGameAchievementRepository, GameAchievementRepository>();
builder.Services.AddScoped<IUserAchievementRepository, UserAchievementRepository>();
builder.Services.AddScoped<ICompetitionRepository, CompetitionRepository>();
builder.Services.AddScoped<ICompetitionPlayerRepository, CompetitionPlayerRepository>();
builder.Services.AddScoped<ICompetitionGameAchievementRepository, CompetitionGameAchievementRepository>();
builder.Services.AddScoped<IBlackListRepository, BlackListRepository>();
builder.Services.AddScoped<IBadgeRepository, BadgeRepository>();
builder.Services.AddScoped<IUserBadgeRepository, UserBadgeRepository>();
builder.Services.AddScoped<IInboxRepository, InboxRepository>();
builder.Services.AddScoped<IIGDBGenresRepository, IGDBGenresRepository>();
builder.Services.AddScoped<IStatusRepository, StatusRepository>();
builder.Services.AddScoped<ICompetitionVoteRepository, CompetitionVoteRepository>();
// Just-in-time provisioning of the paired domain User row (covers Steam-first external login).
builder.Services.AddScoped<IUserProvisioningService, UserProvisioningService>();


builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()                   // enable roles
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Harden the Identity application cookie. SecurePolicy = SameAsRequest keeps local http dev working
// while still requiring HTTPS in production; HttpOnly + SameSite + a sliding expiry are explicit.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    // JSON/AJAX callers under /api get a real 401/403 instead of a 302 to the HTML login page.
    options.Events.OnRedirectToLogin = ApiAwareCookieEvents.RedirectToLogin;
    options.Events.OnRedirectToAccessDenied = ApiAwareCookieEvents.RedirectToAccessDenied;
});

// The security-stamp check rebuilds the cookie principal from the database every 30 minutes,
// which would drop the demo claim (and with it every read-only restriction) mid-session.
// Carry it over to the rebuilt principal.
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.OnRefreshingPrincipal = context =>
    {
        var demoClaim = context.CurrentPrincipal?.FindFirst(SteamProject.Demo.DemoMode.ClaimType);
        if (demoClaim is not null && context.NewPrincipal?.Identity is ClaimsIdentity identity
            && !identity.HasClaim(c => c.Type == SteamProject.Demo.DemoMode.ClaimType))
        {
            identity.AddClaim(new Claim(SteamProject.Demo.DemoMode.ClaimType, demoClaim.Value));
        }
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthentication()
    .AddCookie(options =>
    {
        options.LoginPath = "/Identity/Account/Login";
        options.LogoutPath = "/Identity/Account/Logout";
        options.Events.OnRedirectToLogin = ApiAwareCookieEvents.RedirectToLogin;
        options.Events.OnRedirectToAccessDenied = ApiAwareCookieEvents.RedirectToAccessDenied;
    })
    .AddSteam(options =>
                    {
                        // Env-aware correlation cookie: SameSite=None requires Secure, which is dropped over
                        // plain http — so in Development match the request (keeps local http Steam login
                        // working); in Production keep the strict cross-site settings the OpenID round-trip needs.
                        if (builder.Environment.IsDevelopment())
                        {
                            options.CorrelationCookie.SameSite = SameSiteMode.Lax;
                            options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                        }
                        else
                        {
                            options.CorrelationCookie.SameSite = SameSiteMode.None;
                            options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
                        }
                    });

builder.Services.AddTransient<IEmailSender, EmailSender>();
builder.Services.AddTransient<IInboxService, InboxService>();

builder.Services.AddOpenAIService();

// Secure by default: every endpoint requires an authenticated user unless it opts out
// with [AllowAnonymous]. This closes the systemic auth-bypass on the API controllers
// (Steam/User/Messages/Vote) that previously had no [Authorize] at all.
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Allow the antiforgery token to arrive via a request header so AJAX/fetch callers
// (which cannot post a hidden form field) can satisfy [AutoValidateAntiforgeryToken].
builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");

builder.Services.AddControllersWithViews();
builder.Services.AddSwaggerGen();

// Application Insights for production monitoring
builder.Services.AddApplicationInsightsTelemetry();

// Health checks for monitoring
builder.Services.AddHealthChecks()
    .AddSqlServer(
        steamInfoConnectionString,
        name: "SteamInfoDb",
        tags: new[] { "db", "sql", "steaminfo" })
    .AddSqlServer(
        authConnectionString,
        name: "AuthDb",
        tags: new[] { "db", "sql", "auth" });

var assembly = Assembly.GetExecutingAssembly();
var awardConditionTypes = assembly.GetTypes()
    .Where(t => t.GetInterfaces().Contains(typeof(IAwardCondition)) && !t.IsAbstract);

foreach (var type in awardConditionTypes)
{
    builder.Services.AddScoped(typeof(IAwardCondition), type);
}

builder.Services.AddHostedService<StartupDataInitializer>();

var app = builder.Build();

// Migrations and seeding run in StartupDataInitializer (a BackgroundService) after Kestrel is
// listening, so Azure's container start-up probe is answered within seconds.

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}


// Unknown URLs and other bare 4xx/5xx page responses re-run the pipeline against a friendly page
// (Home/NotFound) that keeps the original status code. /api responses are left alone so JSON
// callers see a bare 401/403/404, not an HTML page.
app.UseWhen(
    context => !ApiAwareCookieEvents.IsApiRequest(context.Request),
    branch => branch.UseStatusCodePagesWithReExecute("/Home/NotFound", "?code={0}"));

app.UseHttpsRedirection();
// Static files (robots.txt, CSS, images) are served before authentication, so they are always
// reachable anonymously and never pass through the auth fallback policy.
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
// Read-only demo sessions: 403 for writes and for pages outside the demo allowlist. Runs before
// authorization so /Admin answers 403 here instead of redirecting to the access-denied page.
app.UseMiddleware<SteamProject.Demo.DemoSessionMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();
app.UseMiddleware<SteamProject.Middlewares.EnsureDomainUserMiddleware>();
app.UseMiddleware<SteamProject.Middlewares.ThemeMiddleware>();
app.UseMiddleware<SteamProject.Middlewares.MessagesMiddleware>();


app.MapControllerRoute(
    "Dashboard",
    "Dashboard/",
    defaults: new { controller = "Home", action = "Dashboard" }
);

app.MapControllerRoute(
    "Compete",
    "Compete",
    defaults: new { controller = "Compete", action = "Index" }
);

app.MapControllerRoute(
    "Compete",
    "Compete/Create",
    defaults: new { controller = "Compete", action = "Create" }
);

app.MapControllerRoute(
    "Compete",
    "Compete/Details/{compId?}",
    defaults: new { controller = "Compete", action = "Details" }
);

app.MapControllerRoute(
    "Compete",
    "Compete/{friendSteamId?}/{appId?}",
    defaults: new { controller = "Compete", action = "Initiate" }
);

app.MapControllerRoute(
    "Friend",
    "Friend/{friendSteamId?}",
    defaults: new { controller = "Friend", action = "Index" }
);

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");


app.MapRazorPages();

// Health checks (all anonymous):
//   /health/live  - liveness: the process is up and serving; runs NO checks, never touches SQL,
//                   so it answers in milliseconds even while the databases are cold or down.
//   /health/ready - readiness: both SQL databases (SteamInfo and Auth) accept a connection;
//                   503 Service Unavailable when either does not.
//   /health       - kept for existing monitors; runs every registered check (same as ready today).
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("db") }).AllowAnonymous();
app.MapHealthChecks("/health").AllowAnonymous();

// Any URL that matches no endpoint is a plain 404 for everyone. Without this, the global
// FallbackPolicy challenged anonymous visitors and made-up URLs redirected to the login page.
// The status-code-pages middleware above turns the bare 404 into the NotFound view.
app.MapFallback("{*path}", context =>
{
    context.Response.StatusCode = StatusCodes.Status404NotFound;
    return Task.CompletedTask;
}).AllowAnonymous();

app.Run();

// Exposed so integration tests can host the app with WebApplicationFactory<Program>.
public partial class Program { }
