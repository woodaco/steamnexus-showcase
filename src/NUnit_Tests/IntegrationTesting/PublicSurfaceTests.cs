using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SteamProject.Services;

namespace NUnit_Tests.IntegrationTesting
{
    /// <summary>
    /// Hosts the real app in memory (WebApplicationFactory) and checks what an anonymous visitor
    /// sees. No database is needed: these paths never reach SQL Server, and the background
    /// migrate/seed service is removed.
    /// </summary>
    public class SteamNexusFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Not Development (no developer exception page or Swagger) and not Production
            // (no appsettings.Production.json).
            builder.UseEnvironment("Testing");
            // Unreachable placeholders; nothing in these tests opens a connection.
            builder.UseSetting("ConnectionStrings:SteamInfoAuthConnection", "Server=127.0.0.1,1;Database=none;User Id=none;Password=none;Connect Timeout=1;TrustServerCertificate=True");
            builder.UseSetting("ConnectionStrings:SteamInfoConnection", "Server=127.0.0.1,1;Database=none;User Id=none;Password=none;Connect Timeout=1;TrustServerCertificate=True");
            builder.ConfigureServices(services =>
            {
                var initializer = services.Where(d => d.ImplementationType == typeof(StartupDataInitializer)).ToList();
                foreach (var d in initializer) services.Remove(d);
            });
        }
    }

    [TestFixture]
    [NonParallelizable]
    public class PublicSurfaceTests
    {
        private SteamNexusFactory _factory = null!;
        private HttpClient _client = null!;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _factory = new SteamNexusFactory();
            _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _client.Dispose();
            _factory.Dispose();
        }

        [Test]
        public async Task AnonymousApiCall_Returns401_Not302()
        {
            var response = await _client.GetAsync("/api/Steam/user");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(response.Headers.Location, Is.Null);
            var body = await response.Content.ReadAsStringAsync();
            Assert.That(body, Does.Not.Contain("<html"), "API errors must not be re-executed into an HTML page");
        }

        [Test]
        public async Task UnknownUrl_Returns404WithNoLocation_AndFriendlyPage()
        {
            var response = await _client.GetAsync("/doesnotexist");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(response.Headers.Location, Is.Null);
            var body = await response.Content.ReadAsStringAsync();
            Assert.That(body, Does.Contain("Page not found"));
        }

        [Test]
        public async Task UnknownApiUrl_Returns404WithoutHtml()
        {
            var response = await _client.GetAsync("/api/doesnotexist");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(response.Headers.Location, Is.Null);
            Assert.That(await response.Content.ReadAsStringAsync(), Does.Not.Contain("<html"));
        }

        [Test]
        public async Task ProtectedPage_StillRedirectsAnonymousVisitorToLogin()
        {
            var response = await _client.GetAsync("/Compete");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
            Assert.That(response.Headers.Location?.ToString(), Does.Contain("/Identity/Account/Login"));
        }

        [Test]
        public async Task RobotsTxt_IsServedAnonymously()
        {
            var response = await _client.GetAsync("/robots.txt");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("User-agent:"));
        }

        [Test]
        public async Task OgImage_IsServedAnonymously()
        {
            var response = await _client.GetAsync("/assets/og.png");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("image/png"));
        }

        [Test]
        public async Task Landing_HasMetaTags_CurrentYear_AndContactLink()
        {
            var response = await _client.GetAsync("/");
            var html = await response.Content.ReadAsStringAsync();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(html, Does.Contain("<meta name=\"description\""));
            Assert.That(html, Does.Contain("property=\"og:title\""));
            Assert.That(html, Does.Contain("property=\"og:description\""));
            Assert.That(html, Does.Match("property=\"og:image\" content=\"[^\"]*/assets/og\\.png\""));
            Assert.That(html, Does.Contain($"© {DateTime.Now.Year}"));
            Assert.That(html, Does.Not.Contain("© 2024"));
            Assert.That(html, Does.Not.Contain("support@steamnexus.com"));
            Assert.That(html, Does.Contain("https://portfolionexus.azurewebsites.net/Cole"));
        }

        [Test]
        public async Task Landing_ForAnonymousVisitor_HasNoLockedNavigation()
        {
            var html = await _client.GetStringAsync("/");

            Assert.That(html, Does.Not.Contain("class=\"vert-nav\""));
            Assert.That(html, Does.Not.Contain("id=\"snOffcanvasNav\""));
            foreach (var locked in new[] { "href=\"/Home/Dashboard\"", "href=\"/Library\"", "href=\"/Compete\"", "href=\"/Home/Friends\"", "href=\"/Identity/Account/Manage\"" })
            {
                Assert.That(html, Does.Not.Contain(locked), $"anonymous landing page should not link to {locked}");
            }
        }

        [Test]
        public async Task Landing_HasNoTestimonialQuote_AndNoDeadGithubLink()
        {
            var html = await _client.GetStringAsync("/");

            Assert.That(html, Does.Not.Contain("class=\"testimonials\""));
            Assert.That(html, Does.Not.Contain("The best platform for achievement hunting"));
            Assert.That(html, Does.Not.Contain("github.com/woodaco/SteamNexus"));
            Assert.That(html, Does.Contain("github.com/woodaco/steamnexus-showcase"));
        }

        [Test]
        public async Task Landing_IsAPortfolioPiece_CapstoneTeamAndSoloRefactor()
        {
            var html = await _client.GetStringAsync("/");

            Assert.That(System.Text.RegularExpressions.Regex.Matches(html, "<h1[\\s>]").Count, Is.EqualTo(1), "exactly one h1");
            Assert.That(html, Does.Contain("capstone"));
            Assert.That(html, Does.Contain("four-person team"));
            Assert.That(html, Does.Contain("refactored solo by Cole Woodard"));
            Assert.That(html, Does.Contain("id=\"my-role\""));
            Assert.That(html, Does.Contain("draft, to be confirmed"));
            Assert.That(html, Does.Contain("id=\"built-with\""));
            Assert.That(html, Does.Not.Match("class=\"[^\"]*\\bcarousel\\b"));
            Assert.That(html, Does.Not.Contain("landingCarousel"));
            Assert.That(html, Does.Not.Contain("testimonial"));
            Assert.That(html, Does.Not.Match("\\d+\\s?% of (the )?commits"));
        }

        [Test]
        public async Task Landing_Screenshots_HaveAltText_AndAreServed()
        {
            var html = await _client.GetStringAsync("/");
            var start = html.IndexOf("id=\"screenshots\"", StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThan(-1), "#screenshots section present");
            var end = html.IndexOf("</section>", start, StringComparison.Ordinal);
            var section = html.Substring(start, end - start);

            var imgs = System.Text.RegularExpressions.Regex.Matches(section, "<img\\b[^>]*>");
            Assert.That(imgs.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(section, Does.Not.Contain("\"~/"), "app-relative URLs are resolved");
            foreach (System.Text.RegularExpressions.Match img in imgs)
            {
                Assert.That(img.Value, Does.Match("alt=\"[^\"]{20,}\""), $"meaningful alt text on {img.Value}");
                var src = System.Text.RegularExpressions.Regex.Match(img.Value, "src=\"([^\"?]+)").Groups[1].Value;
                var response = await _client.GetAsync(src);
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), src);
                Assert.That(response.Content.Headers.ContentLength, Is.GreaterThan(1000), src);
            }
        }

        [Test]
        public async Task Privacy_IsPublic_AndSaysItIsAStudentCapstone()
        {
            var response = await _client.GetAsync("/Home/Privacy");
            var html = await response.Content.ReadAsStringAsync();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(html, Does.Contain("capstone"));
            Assert.That(html, Does.Contain("Steam ID"));
            Assert.That(html, Does.Contain("No marketing"));
        }
    }
}
