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
    public class KODO100StepDefinitions
    {
        private IConfigurationRoot Configuration { get; }

        private readonly ScenarioContext _scenarioContext;
        private readonly HomePageObject _homePage;
        private readonly ProfilePageObject _profilePage;
        private readonly FriendPageObject _friendPage;
        private readonly FriendsPageObject _friendsPage;

        public KODO100StepDefinitions(ScenarioContext context, BrowserDriver browserDriver)
        {
            _homePage = new HomePageObject(browserDriver.Current);
            _profilePage = new ProfilePageObject(browserDriver.Current);
            _friendPage = new FriendPageObject(browserDriver.Current);
            _friendsPage = new FriendsPageObject(browserDriver.Current);


            _scenarioContext = context;

            IConfigurationBuilder builder = new ConfigurationBuilder()
                .AddUserSecrets<KODO129StepDefinitions>()
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        [When(@"I click on ""(.*)'s"" friend page link")]
        public void WhenIClickOnFriendsPageLink(string friendName)
        {
            // Wait for page to load after navigating to Profile
            Thread.Sleep(1000);
            // Navigate to Friends page to find the friend
            _friendsPage.GoTo();
            // Wait for friends page to fully load
            Thread.Sleep(2000);
            // Click on the friend's compare-games button to see shared games
            _friendsPage.GoToFriendSharedGames(friendName);
            // Wait for the friend page to load
            Thread.Sleep(2000);
        }

        [Then(@"I can see the shared games page for ""(.*)""")]
        public void ThenISeeSharedGamesForFriend(string friendName)
        {
            var sharedGames = _friendPage.GetSharedGames();
            sharedGames.Should().NotBeNullOrEmpty("No shared games found for friend: " + friendName);
        }


        [Then(@"I can see ""(.*)'s"" username")]
        public void ThenICanSeeFriendUsername(string friendName)
        {
            _friendPage.GetFriendUsername().GetAttribute("innerHTML").Should().ContainEquivalentOf(friendName, AtLeast.Once());
        }


    }
}
