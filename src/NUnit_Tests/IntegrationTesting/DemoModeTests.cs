using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using SteamProject.Areas.Identity.Data;
using SteamProject.Demo;
using SteamProject.Models;
using SteamProject.Services;

namespace NUnit_Tests.IntegrationTesting
{
    /// <summary>
    /// Hosts the app in memory with both databases swapped for EF InMemory stores, so a demo
    /// sign-in and the database-backed pages can be exercised without SQL Server.
    /// </summary>
    public class DemoFactory : WebApplicationFactory<Program>
    {
        private readonly bool _demoEnabled;
        private readonly string _dbName = "demo-" + Guid.NewGuid().ToString("N");

        public DemoFactory(bool demoEnabled)
        {
            _demoEnabled = demoEnabled;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:SteamInfoAuthConnection", "Server=127.0.0.1,1;Database=none;User Id=none;Password=none;Connect Timeout=1;TrustServerCertificate=True");
            builder.UseSetting("ConnectionStrings:SteamInfoConnection", "Server=127.0.0.1,1;Database=none;User Id=none;Password=none;Connect Timeout=1;TrustServerCertificate=True");
            builder.UseSetting(DemoMode.EnabledKey, _demoEnabled ? "true" : "false");
            builder.ConfigureServices(services =>
            {
                foreach (var d in services.Where(d => d.ImplementationType == typeof(StartupDataInitializer)).ToList())
                    services.Remove(d);
                foreach (var d in services.Where(d =>
                             d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                             d.ServiceType == typeof(DbContextOptions<SteamInfoDbContext>)).ToList())
                    services.Remove(d);
                services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(_dbName + "-auth"));
                services.AddDbContext<SteamInfoDbContext>(o => o.UseLazyLoadingProxies().UseInMemoryDatabase(_dbName + "-info"));
            });
        }

        /// <summary>A small game catalogue plus the demo persona, seeded by the production seeder.</summary>
        public async Task SeedAsync()
        {
            using var scope = Services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<SteamInfoDbContext>();
            if (!ctx.Games.Any())
            {
                for (var i = 1; i <= 10; i++)
                    ctx.Games.Add(new Game { AppId = 1000 + i, Name = $"Catalogue Game {i}", Genres = "Action" });
                ctx.SaveChanges();
            }
            await DemoPersonaSeeder.ReseedAsync(scope.ServiceProvider);

            // Like Development, where AdminEmails promotes the seeded account to admin.
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            if (!await roles.RoleExistsAsync("admin")) await roles.CreateAsync(new IdentityRole("admin"));
            var persona = await users.FindByEmailAsync(DemoMode.PersonaEmail);
            if (!await users.IsInRoleAsync(persona!, "admin")) await users.AddToRoleAsync(persona!, "admin");
        }
    }

    internal static class DemoHttp
    {
        private static readonly Regex TokenPattern =
            new("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"", RegexOptions.Compiled);

        public static string Token(string html)
        {
            var m = TokenPattern.Match(html);
            Assert.That(m.Success, Is.True, "page carries an antiforgery token");
            return m.Groups[1].Value;
        }

        public static async Task<HttpResponseMessage> PostWithTokenAsync(HttpClient client, string tokenPage, string url)
        {
            var token = Token(await client.GetStringAsync(tokenPage));
            return await client.PostAsync(url, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
            }));
        }
    }

    [TestFixture]
    [NonParallelizable]
    public class DemoModeDisabledTests
    {
        private DemoFactory _factory = null!;
        private HttpClient _client = null!;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _factory = new DemoFactory(demoEnabled: false);
            _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _client.Dispose();
            _factory.Dispose();
        }

        [Test]
        public async Task Enter_WithFlagOff_Returns404()
        {
            var response = await DemoHttp.PostWithTokenAsync(_client, "/", "/Demo/Enter");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(response.Headers.Location, Is.Null);
        }

        [Test]
        public async Task Landing_WithFlagOff_HasNoDemoButton()
        {
            var html = await _client.GetStringAsync("/");

            Assert.That(html, Does.Not.Contain("id=\"try-demo\""));
            Assert.That(html, Does.Not.Contain("Try the demo"));
            Assert.That(html, Does.Not.Contain("/Demo/Enter"));
        }
    }

    [TestFixture]
    [NonParallelizable]
    public class DemoModeEnabledTests
    {
        private DemoFactory _factory = null!;

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            _factory = new DemoFactory(demoEnabled: true);
            _factory.CreateClient().Dispose(); // build the host
            await _factory.SeedAsync();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown() => _factory.Dispose();

        private HttpClient NewClient() =>
            _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        private async Task<HttpClient> EnterDemoAsync()
        {
            var client = NewClient();
            var response = await DemoHttp.PostWithTokenAsync(client, "/", "/Demo/Enter");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect), "Enter redirects after signing in");
            Assert.That(response.Headers.Location?.ToString(), Is.EqualTo("/Dashboard"));
            return client;
        }

        [Test]
        public async Task Landing_WithFlagOn_ShowsDemoButtonToAnonymousVisitor()
        {
            using var client = NewClient();
            var html = await client.GetStringAsync("/");

            Assert.That(html, Does.Contain("id=\"try-demo\""));
            Assert.That(html, Does.Contain("Try the demo"));
            Assert.That(html, Does.Match("<form[^>]*action=\"/Demo/Enter\""));
        }

        [Test]
        public async Task Enter_SignsIn_AndDashboardShowsBannerAndSeededGames()
        {
            using var client = await EnterDemoAsync();

            var response = await client.GetAsync("/Dashboard");
            var html = await response.Content.ReadAsStringAsync();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(html, Does.Contain(DemoMode.BannerText));
            Assert.That(Regex.Matches(html, "class=\"card h-100 userCard\"").Count, Is.GreaterThanOrEqualTo(5), "5 or more seeded games");
            Assert.That(html, Does.Not.Contain("ShowMoreNews"), "no news links (they would call Steam)");
            Assert.That(html, Does.Not.Contain("dashboardGameNews.js"), "no news script (it would call Steam and OpenAI)");
            Assert.That(html, Does.Not.Contain("id=\"navProfile\""));
            Assert.That(html, Does.Not.Contain("navAdmin"), "an admin role on the persona does not reach a demo session");
            Assert.That(html, Does.Contain("id=\"demo-exit\""));
        }

        [Test]
        public async Task Enter_IssuesANonPersistentCookie()
        {
            var client = NewClient();
            var token = DemoHttp.Token(await client.GetStringAsync("/"));
            var response = await client.PostAsync("/Demo/Enter", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));

            var identityCookie = response.Headers.GetValues("Set-Cookie").FirstOrDefault(c => c.StartsWith(".AspNetCore.Identity.Application="));
            Assert.That(identityCookie, Is.Not.Null, "signed in with the normal Identity cookie");
            Assert.That(identityCookie, Does.Not.Contain("expires=").IgnoreCase, "session cookie, gone when the browser closes");
            client.Dispose();
        }

        [Test]
        public async Task LibraryAndCompete_Render_ForDemoSession()
        {
            using var client = await EnterDemoAsync();

            foreach (var path in new[] { "/Library", "/Compete" })
            {
                var response = await client.GetAsync(path);
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), path);
                Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain(DemoMode.BannerText), path);
            }
        }

        [TestCase("/Compete/Create")]
        [TestCase("/Identity/Account/Logout")]
        [TestCase("/api/Messages/sendMessageTo")]
        [TestCase("/api/Vote/competitionvote")]
        [TestCase("/Admin/Index")]
        [TestCase("/Demo/Enter")]
        public async Task AnyPostOtherThanExit_Returns403_ForDemoSession(string path)
        {
            using var client = await EnterDemoAsync();
            var token = DemoHttp.Token(await client.GetStringAsync("/Dashboard"));

            var response = await client.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden), path);
        }

        [Test]
        public async Task PutAndDelete_Return403_ForDemoSession()
        {
            using var client = await EnterDemoAsync();

            Assert.That((await client.PutAsync("/api/Steam/follow?id=1", new StringContent(""))).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That((await client.DeleteAsync("/api/Steam/DeleteComp?compId=1")).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        }

        [TestCase("/Admin")]
        [TestCase("/Identity/Account/Manage")]
        [TestCase("/Inbox")]
        [TestCase("/Compete/Create")]
        [TestCase("/Compete/76561198000000003/1001")]
        [TestCase("/Home/Friends")]
        [TestCase("/Home/ShowMoreNews?appId=1001")]
        [TestCase("/Library/ShowMoreInfo?appId=1001")]
        [TestCase("/api/News/GetRecentGameNews?appId=1001")]
        [TestCase("/api/Steam/user")]
        public async Task DeniedPages_Return403_ForDemoSession(string path)
        {
            using var client = await EnterDemoAsync();

            var response = await client.GetAsync(path);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden), path);
            Assert.That(response.Headers.Location, Is.Null, "a 403, not a redirect to the access-denied page");
        }

        [Test]
        public async Task DeniedPage_ExplainsTheDemoIsReadOnly()
        {
            using var client = await EnterDemoAsync();

            var html = await (await client.GetAsync("/Admin")).Content.ReadAsStringAsync();

            Assert.That(html, Does.Contain("Not available in the demo"));
            Assert.That(html, Does.Contain(DemoMode.BannerText));
        }

        [Test]
        public async Task Exit_SignsOut_AndEndsTheDemoSession()
        {
            using var client = await EnterDemoAsync();

            var response = await DemoHttp.PostWithTokenAsync(client, "/Dashboard", "/Demo/Exit");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
            Assert.That(response.Headers.Location?.ToString(), Is.EqualTo("/"));

            var after = await client.GetAsync("/Dashboard");
            Assert.That(after.StatusCode, Is.EqualTo(HttpStatusCode.Redirect), "back to anonymous");
            Assert.That(after.Headers.Location?.ToString(), Does.Contain("/Identity/Account/Login"));
        }

        [Test]
        public async Task Persona_IsSeededInDemoRole_WithEnoughGamesAndAnAvatar()
        {
            using var scope = _factory.Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var ctx = scope.ServiceProvider.GetRequiredService<SteamInfoDbContext>();

            var identity = await userManager.FindByEmailAsync(DemoMode.PersonaEmail);
            Assert.That(identity, Is.Not.Null);
            Assert.That(await userManager.IsInRoleAsync(identity!, DemoMode.RoleName), Is.True);
            Assert.That(await userManager.HasPasswordAsync(identity!), Is.False, "created by the demo seeder: no password to guess");

            var persona = ctx.Users.Single(u => u.AspNetUserId == identity!.Id);
            Assert.That(persona.AvatarUrl, Is.Not.Null, "Library never asks Steam for an avatar");
            Assert.That(ctx.UserGameInfos.Count(u => u.OwnerId == persona.Id), Is.GreaterThanOrEqualTo(DemoMode.MinimumGames));

            // Idempotent.
            await DemoPersonaSeeder.ReseedAsync(scope.ServiceProvider);
            Assert.That(ctx.UserGameInfos.Count(u => u.OwnerId == persona.Id), Is.EqualTo(DemoMode.MinimumGames));
        }

        [Test]
        public void SecurityStampRefresh_KeepsTheDemoClaim()
        {
            var options = _factory.Services.GetRequiredService<IOptions<SecurityStampValidatorOptions>>().Value;
            var current = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(DemoMode.ClaimType, "123") }, "test"));
            var rebuilt = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, DemoMode.PersonaEmail) }, "test"));

            options.OnRefreshingPrincipal!(new SecurityStampRefreshingPrincipalContext { CurrentPrincipal = current, NewPrincipal = rebuilt });

            Assert.That(rebuilt.FindFirst(DemoMode.ClaimType)?.Value, Is.EqualTo("123"));
        }
    }

    [TestFixture]
    public class DemoUnitTests
    {
        private static ClaimsPrincipal DemoPrincipal(DateTimeOffset expires) =>
            new(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "x"),
                new Claim(DemoMode.ClaimType, expires.ToUnixTimeSeconds().ToString()),
            }, "test"));

        [TestCase("GET", "/Dashboard", true)]
        [TestCase("GET", "/dashboard/", true)]
        [TestCase("GET", "/Compete", true)]
        [TestCase("GET", "/Compete/Details/4", true)]
        [TestCase("GET", "/Library", true)]
        [TestCase("HEAD", "/Library", true)]
        [TestCase("POST", "/Demo/Exit", true)]
        [TestCase("POST", "/Demo/Enter", false)]
        [TestCase("POST", "/Dashboard", false)]
        [TestCase("PATCH", "/Dashboard", false)]
        [TestCase("GET", "/Admin", false)]
        [TestCase("GET", "/Identity/Account/Manage/Email", false)]
        [TestCase("GET", "/Inbox/Index", false)]
        [TestCase("GET", "/Compete/Create", false)]
        [TestCase("GET", "/Compete/123/456", false)]
        [TestCase("GET", "/Compete/FriendsList", false)]
        public void IsAllowed_MatchesTheReadOnlyAllowlist(string method, string path, bool allowed)
        {
            Assert.That(DemoSessionMiddleware.IsAllowed(method, new PathString(path)), Is.EqualTo(allowed));
        }

        [Test]
        public void IsExpired_TrueOnlyAfterTheClaimedExpiry()
        {
            var now = DateTimeOffset.UtcNow;
            Assert.That(DemoMode.IsExpired(DemoPrincipal(now.AddMinutes(5)), now), Is.False);
            Assert.That(DemoMode.IsExpired(DemoPrincipal(now.AddMinutes(-1)), now), Is.True);
            Assert.That(DemoMode.IsExpired(DemoPrincipal(now.AddMinutes(60)), now.AddMinutes(61)), Is.True);
        }

        [Test]
        public void StripRolesOtherThanDemo_LeavesOnlyTheDemoRole()
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Role, "admin"),
                new Claim(ClaimTypes.Role, DemoMode.RoleName),
            }, "test"));

            DemoSessionMiddleware.StripRolesOtherThanDemo(user);

            Assert.That(user.IsInRole("admin"), Is.False);
            Assert.That(user.IsInRole(DemoMode.RoleName), Is.True);
        }

        [Test]
        public void SessionLength_IsSixtyMinutes()
        {
            Assert.That(DemoMode.SessionLength, Is.EqualTo(TimeSpan.FromMinutes(60)));
        }

        [Test]
        public async Task ExpiredDemoSession_IsSignedOutAndRedirected()
        {
            var services = new ServiceCollection().AddLogging().AddAuthentication(o => o.DefaultScheme = IdentityConstants.ApplicationScheme)
                .AddCookie(IdentityConstants.ApplicationScheme).Services.BuildServiceProvider();
            var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { [DemoMode.EnabledKey] = "true" }).Build();
            var context = new DefaultHttpContext { RequestServices = services, User = DemoPrincipal(DateTimeOffset.UtcNow.AddMinutes(-1)) };
            context.Request.Method = "GET";
            context.Request.Path = "/Dashboard";
            var reachedNext = false;
            var middleware = new DemoSessionMiddleware(_ => { reachedNext = true; return Task.CompletedTask; });

            await middleware.InvokeAsync(context, config);

            Assert.That(reachedNext, Is.False);
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status302Found));
            Assert.That(context.Response.Headers.Location.ToString(), Is.EqualTo("/"));
        }

        [Test]
        public async Task SteamGuard_NeverCallsSteam_ForDemoSession_AndAnswersFromTheDatabase()
        {
            var dbName = "guard-" + Guid.NewGuid();
            var sp = new ServiceCollection()
                .AddDbContext<SteamInfoDbContext>(o => o.UseInMemoryDatabase(dbName))
                .BuildServiceProvider();
            using (var seed = sp.CreateScope())
            {
                var ctx = seed.ServiceProvider.GetRequiredService<SteamInfoDbContext>();
                ctx.Users.Add(new User { AspNetUserId = "a", SteamId = "1", SteamName = "DemoPlayer", AvatarUrl = "/assets/person.png", PlayerLevel = 12 });
                ctx.SaveChanges();
            }
            using var scope = sp.CreateScope();
            var inner = new Mock<ISteamService>(MockBehavior.Strict);
            var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = DemoPrincipal(DateTimeOffset.UtcNow.AddMinutes(30)) } };
            var guard = new DemoGuardSteamService(inner.Object, accessor, scope.ServiceProvider);

            var users = await guard.GetManyUsersAsync(new List<string> { "1", "2" });
            var level = await guard.GetUserLevelAsync("1");
            var games = await guard.GetGamesAsync("1", 1);
            var achievements = await guard.GetAchievementsAsync("1", 10);
            Assert.ThrowsAsync<InvalidOperationException>(() => guard.GetGameInfoAsync(new Game { Name = "x" }));

            Assert.That(users.Select(u => u.SteamName), Is.EqualTo(new[] { "DemoPlayer", null }));
            Assert.That(level, Is.EqualTo(12));
            Assert.That(games, Is.Empty);
            Assert.That(achievements, Is.Null);
            inner.VerifyNoOtherCalls();
        }

        [Test]
        public async Task SteamGuard_PassesThrough_ForNormalSession()
        {
            var inner = new Mock<ISteamService>();
            inner.Setup(s => s.GetUserLevelAsync("1")).ReturnsAsync(42);
            var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
            var guard = new DemoGuardSteamService(inner.Object, accessor, new ServiceCollection().BuildServiceProvider());

            Assert.That(await guard.GetUserLevelAsync("1"), Is.EqualTo(42));
        }

        [Test]
        public async Task OpenAiGuard_NeverCallsOpenAi_ForDemoSession()
        {
            var inner = new Mock<IOpenAiApiService>(MockBehavior.Strict);
            var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = DemoPrincipal(DateTimeOffset.UtcNow.AddMinutes(30)) } };
            var guard = new DemoGuardOpenAiService(inner.Object, accessor);

            Assert.That(await guard.SummarizeNewsShortAsync("news"), Does.Contain("turned off"));
            inner.VerifyNoOtherCalls();
        }
    }
}
