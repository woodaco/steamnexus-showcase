using BDD_Tests.Drivers;
using BDD_Tests.Shared;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BDD_Tests.PageObjects;
using OpenQA.Selenium;
using BoDi;
using OpenQA.Selenium.Support.UI;
using BDD_Tests.StepDefinitions;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace BDD_Tests.Hooks
{
    /// <summary>
    /// Share the same browser window for all scenarios
    /// </summary>
    /// <remarks>
    /// This makes the sequential execution of scenarios faster (opening a new browser window each time would take more time)
    /// As a tradeoff:
    ///  - we cannot run the tests in parallel
    ///  - we have to "reset" the state of the browser before each scenario
    /// </remarks>
    [Binding]
    public class SharedBrowserHooks
    {
        private static IObjectContainer _objectContainer;
        private static BrowserDriver _browserDriver;

        private IConfigurationRoot Configuration { get; }

        public SharedBrowserHooks(ScenarioContext context, IObjectContainer objectContainer)
        {
            _objectContainer = objectContainer;

            IConfigurationBuilder builder = new ConfigurationBuilder()
                .AddUserSecrets<SharedBrowserHooks>()
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }
        // How long to keep probing the site before failing the run. An Azure App Service
        // cold start can take minutes, so CI waits; override with BDD_SERVER_WAIT_SECONDS.
        private static readonly TimeSpan ServerWait = TimeSpan.FromSeconds(
            int.TryParse(Environment.GetEnvironmentVariable("BDD_SERVER_WAIT_SECONDS"), out var s) && s > 0 ? s : 300);

        [BeforeTestRun]
        public static void BeforeTestRun(IObjectContainer container)
        {
            // An unreachable server FAILS the run. It used to Assert.Ignore every scenario,
            // which let a cold or broken deploy pass with zero BDD tests executed.
            WaitForServer(Common.BaseUrl, ServerWait);

            _browserDriver = new BrowserDriver();
            container.RegisterInstanceAs<BrowserDriver>(_browserDriver);
        }

        private static void WaitForServer(string url, TimeSpan timeout)
        {
            var handler = new System.Net.Http.HttpClientHandler();
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.IsLoopback)
            {
                // The local dev server uses a self-signed development certificate.
                handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
            }

            using var client = new System.Net.Http.HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            string lastResult = "no response";

            while (stopwatch.Elapsed < timeout)
            {
                try
                {
                    var response = client.GetAsync(url).GetAwaiter().GetResult();
                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"BDD target {url} is up after {stopwatch.Elapsed.TotalSeconds:F0}s");
                        return;
                    }
                    lastResult = $"HTTP {(int)response.StatusCode}";
                }
                catch (Exception ex)
                {
                    lastResult = ex.GetType().Name + ": " + ex.Message;
                }

                Console.WriteLine($"Waiting for BDD target {url} ({lastResult})");
                System.Threading.Thread.Sleep(TimeSpan.FromSeconds(5));
            }

            throw new InvalidOperationException(
                $"BDD target {url} was not reachable within {timeout.TotalSeconds:F0}s (last: {lastResult}). " +
                "Failing the run rather than skipping every scenario.");
        }

        [AfterTestRun]
        public static void AfterTestRun()
        {
            if (_browserDriver != null)
            {
                _browserDriver.Dispose();
            }
        }


        [AfterScenario]
        public static void AfterScenario()
        {
            // No-op; browser disposed in AfterTestRun.
        }


        [AfterScenario("@RevertNickname")]
        public void TearDown()
        {
            if (_objectContainer == null) return;

            var browserDriver = _objectContainer.Resolve<BrowserDriver>();
            if (browserDriver == null || !browserDriver.IsWebDriverCreated()) return;

            var webDriver = browserDriver.WebDriver;
            var friendsPage = new FriendsPageObject(webDriver);

            var allFriendIds = friendsPage.GetAllFriendSteamIds();

            foreach (var friendId in allFriendIds)
            {
                try
                {
                    friendsPage.Revert(friendId);
                }
                catch (NoSuchElementException)
                {
                    // Do nothing if the revert icon isn't found for a friend
                }
                catch (ObjectDisposedException)
                {
                    // Browser already disposed — stop iterating.
                    break;
                }
            }
        }

        [BeforeScenario("@SetupAlias")]
        public void SetupAlias()
        {
            if (_objectContainer == null) return;

            var browserDriver = _objectContainer.Resolve<BrowserDriver>();
            if (browserDriver == null || !browserDriver.IsWebDriverCreated()) return;

            var webDriver = browserDriver.WebDriver;

            // Navigate to the Login page
            webDriver.Navigate().GoToUrl($"{Common.BaseUrl}/Identity/Account/Login");

            var loginPage = new LoginPageObject(webDriver);

            var user = new TestUser
            {
                UserName = "TestUser",
                FirstName = "The",
                LastName = "Admin",
                Email = "TestUser@mail.com",
                Password = Configuration["SeedUserPW"]
            };

            if (user.Password == null)
            {
                throw new Exception("Did you forget to set the admin password in user-secrets?");
            }

            loginPage.EnterEmail(user.Email);
            loginPage.EnterPassword(user.Password);
            loginPage.Login();

            // Navigate to the Friends page
            webDriver.Navigate().GoToUrl($"{Common.BaseUrl}/Home/Friends");

            var friendsPage = new FriendsPageObject(webDriver);
            friendsPage.GiveNewName("Steve", "Minecraft");

            // Log out
            friendsPage.Logout();  // Assuming the Logout method is in your FriendsPageObject

            // Navigate back to the Login page
            webDriver.Navigate().GoToUrl($"{Common.BaseUrl}/Identity/Account/Login");
        }





    }
}
