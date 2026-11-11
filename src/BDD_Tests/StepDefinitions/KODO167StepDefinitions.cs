using BDD_Tests.Drivers;
using BDD_Tests.PageObjects;
using Microsoft.Extensions.Configuration;
using System;
using System.Diagnostics;
using TechTalk.SpecFlow.Assist;


using NUnit.Framework;

namespace BDD_Tests.StepDefinitions
{
    [Binding]
    [Parallelizable(ParallelScope.None)]
    public class KODO167StepDefinitions
    {

        private IConfigurationRoot Configuration { get; }

        private readonly ScenarioContext _scenarioContext;
        private readonly FriendsPageObject _friendsPage;
        private readonly LoginPageObject _loginPage;


        public KODO167StepDefinitions(ScenarioContext context, BrowserDriver browserDriver)
        {
            _loginPage = new LoginPageObject(browserDriver.Current);
            _friendsPage = new FriendsPageObject(browserDriver.Current);
            _scenarioContext = context;

            IConfigurationBuilder builder = new ConfigurationBuilder()
                .AddUserSecrets<KODO167StepDefinitions>()
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }
        [Given(@"I click on the envelope icon")]
        public void GivenIClickOnTheEnvelopeIcon()
        {
            Thread.Sleep(500);
            _friendsPage.Invite.Click();

        }

        [When(@"I enter a valid email ""([^""]*)""")]
        public void WhenIEnterAValidEmail(string address)
        {
            Thread.Sleep(500);
            _friendsPage.EmailBox.Click();
            Thread.Sleep(500);
            _friendsPage.EmailBox.SendKeys("testuser@mail.com");
            Thread.Sleep(500);
            _friendsPage.SendBtn.Click();
        }

        [Then(@"I should see a success message and the modal has closed")]
        public void ThenIShouldSeeASuccessMessageAndTheModalHasClosed()
        {
            // Wait for the modal to close after the success message
            // The JS code sets display:none after showing success message
            Thread.Sleep(3000);
            _friendsPage.WaitForInviteModalToClose().Should().BeTrue("the invite modal should be closed after sending email");
        }
    }
}
