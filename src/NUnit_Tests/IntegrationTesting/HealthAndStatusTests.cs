using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using SteamProject.Services;

namespace NUnit_Tests.IntegrationTesting
{
    /// <summary>
    /// M5: liveness never depends on SQL, readiness does, and /status shows the build stamp.
    /// The factory points both connection strings at an unreachable port, so readiness must fail.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class HealthAndStatusTests
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
        public async Task HealthLive_Returns200Anonymously_FastWithoutDatabase()
        {
            await _client.GetAsync("/health/live"); // warm the host
            var timer = Stopwatch.StartNew();
            var response = await _client.GetAsync("/health/live");
            timer.Stop();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Headers.Location, Is.Null);
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("Healthy"));
            Assert.That(timer.Elapsed, Is.LessThan(TimeSpan.FromSeconds(1)));
        }

        [Test]
        public async Task HealthReady_Returns503Anonymously_WhenDatabasesAreUnreachable()
        {
            var response = await _client.GetAsync("/health/ready");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
            Assert.That(response.Headers.Location, Is.Null);
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("Unhealthy"));
        }

        [Test]
        public async Task Status_IsAnonymous_AndShowsShaAndEnvironment()
        {
            var response = await _client.GetAsync("/status");
            var html = await response.Content.ReadAsStringAsync();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Headers.Location, Is.Null);
            Assert.That(html, Does.Contain($"<code id=\"status-sha\">{BuildInfo.GitSha}</code>"));
            Assert.That(html, Does.Not.Contain("status-built"), "build time is not published");
            Assert.That(html, Does.Contain("<dd id=\"status-env\">Testing</dd>"));
            Assert.That(html, Does.Not.Contain("Password="), "status must never print connection strings");
            Assert.That(html, Does.Not.Contain("Server="));
        }

        [Test]
        public void BuildInfo_IsStampedAtBuildTime()
        {
            Assert.That(BuildInfo.GitSha, Does.Match("^([0-9a-f]{7,40}|unknown)$"));
            Assert.That(BuildInfo.BuildTimeUtc, Does.Match(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$"));
        }

        [TestCase(null, true)]
        [TestCase("true", true)]
        [TestCase("false", false)]
        [TestCase("False", false)]
        public void ShouldSeed_DefaultsOn_AndHonoursConfigFlag(string? value, bool expected)
        {
            var settings = new Dictionary<string, string?>();
            if (value is not null) settings[StartupDataInitializer.RunSeedingKey] = value;
            var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

            Assert.That(StartupDataInitializer.ShouldSeed(config), Is.EqualTo(expected));
        }
    }
}
