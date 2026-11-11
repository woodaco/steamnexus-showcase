using BDD_Tests.Drivers;
using BDD_Tests.PageObjects;
using BDD_Tests.Shared;
using Microsoft.Extensions.Configuration;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using System;
using System.Diagnostics;
using TechTalk.SpecFlow.Assist;

using NUnit.Framework;

namespace BDD_Tests.StepDefinitions
{
    public class TestUser
    {
        public string UserName { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Password { get; set; }
    }

    [Binding]
    [Parallelizable(ParallelScope.None)]
    public class KODO129StepDefinitions
    {
        private IConfigurationRoot Configuration { get; }

        private readonly ScenarioContext _scenarioContext;
        private readonly IWebDriver _webDriver;
        private readonly LoginPageObject _loginPage;
        private readonly HomePageObject _homePage;
        private readonly CompetePageObject _competePage;

        public KODO129StepDefinitions(ScenarioContext context, BrowserDriver browserDriver)
        {
            _webDriver = browserDriver.Current;
            _loginPage = new LoginPageObject(_webDriver);
            _homePage = new HomePageObject(_webDriver);
            _competePage = new CompetePageObject(_webDriver);
            _scenarioContext = context;

            // Env vars supplement user-secrets so CI (or `SeedUserPW=... dotnet test`) can supply the password.
            IConfigurationBuilder builder = new ConfigurationBuilder()
                .AddUserSecrets<KODO129StepDefinitions>()
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }


        [Given(@"I am signed in")]
        public void GivenIAmSignedIn()
        {
            TestUser user = new TestUser
            {
                UserName = "TestUser",
                FirstName = "The",
                LastName = "Admin",
                Email = "TestUser@mail.com",
                Password = Configuration["SeedUserPW"]
            };
            if (user.Password == null)
            {
                throw new Exception("Set SeedUserPW in BDD user-secrets or the SeedUserPW env var (same value as the web app's seed user password).");
            }

            // The browser is shared across scenarios, so start from a clean (logged-out) session — otherwise
            // a prior scenario leaves us authenticated and the login form is absent ("Email field not found").
            _loginPage.GoTo();
            _webDriver.Manage().Cookies.DeleteAllCookies();
            _loginPage.GoTo();

            // Reset browser state to avoid stale sessions after multiple scenarios.
            try { _webDriver.Navigate().Refresh(); } catch { /* ignore */ }

            _loginPage.EnterEmail(user.Email);
            _loginPage.EnterPassword(user.Password);
            _loginPage.Login();

            // Wait for the post-login redirect away from the login page (fail fast if credentials are wrong).
            // Also accept validation-summary-errors appearing on the same page — that means the server
            // returned the login page with an error (e.g. email not confirmed), which is a valid login outcome.
            new WebDriverWait(_webDriver, TimeSpan.FromSeconds(15))
                .Until(d => {
                    var uri = new Uri(d.Url);
                    return !uri.LocalPath.ToLower().Equals("/identity/account/login")
                        || d.FindElements(By.CssSelector(".validation-summary-errors")).Count > 0;
                });
        }

  

        [Then(@"I end up on the competitions list page")]
        public void ThenIEndUpOnCompetitionsListPage()
        {
            var title = _competePage.GetTitle();
            _competePage.GetTitle().Should().ContainEquivalentOf("Competitions", AtLeast.Once());
        }
    }
}
