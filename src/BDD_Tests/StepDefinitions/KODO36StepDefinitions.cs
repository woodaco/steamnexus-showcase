using BDD_Tests.Drivers;
using BDD_Tests.PageObjects;
using Microsoft.Extensions.Configuration;
using System;
using System.Diagnostics;
using TechTalk.SpecFlow;

using NUnit.Framework;

namespace BDD_Tests.StepDefinitions
{
    [Binding]
    [Parallelizable(ParallelScope.None)]
    public class KODO36StepDefinitions
    {
        private IConfigurationRoot Configuration { get; }

        private readonly HomePageObject _homePage;
        private readonly ScenarioContext _scenarioContext;
        private readonly LibraryPageObject _libraryPage;
        private readonly LoginPageObject _loginPage;

        public KODO36StepDefinitions(ScenarioContext context, BrowserDriver browserDriver)
        {
            _scenarioContext = context;
            _homePage = new HomePageObject(browserDriver.Current);
            _libraryPage = new LibraryPageObject(browserDriver.Current);
            _loginPage = new LoginPageObject(browserDriver.Current);

            IConfigurationBuilder builder = new ConfigurationBuilder()
                .AddUserSecrets<KODO30StepDefinitions>()
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        [When(@"I click on the hidden modal button")]
        public void WhenIClickOnTheHiddenModalButton()
        {
            // Wait for the library page to fully load
            Thread.Sleep(1000);
            _libraryPage.ShowHiddenModal();
        }

        [Then(@"I should see the empty hidden modal")]
        public void ThenIShouldSeeTheEmptyHiddenModal()
        {
            _libraryPage.GetEmptyModalMessage().Should().BeTrue("The empty hidden modal message should be visible when there are no hidden games");
        }
    }
}
