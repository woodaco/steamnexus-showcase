using BDD_Tests.Drivers;
using BDD_Tests.PageObjects;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using System;
using System.Diagnostics;
using TechTalk.SpecFlow.Assist;

namespace BDD_Tests.StepDefinitions
{
    [Binding]
    [Parallelizable(ParallelScope.None)]
    public class KODO263StepDefinitions
    {
        private IConfigurationRoot Configuration { get; }

        private readonly ScenarioContext _scenarioContext;
        private readonly CompetePageObject _competePage;
        private readonly IWebDriver _webDriver;

        public KODO263StepDefinitions(ScenarioContext context, BrowserDriver browserDriver)
        {
            _competePage = new CompetePageObject(browserDriver.Current);
            _webDriver = browserDriver.Current;
            _scenarioContext = context;

            IConfigurationBuilder builder = new ConfigurationBuilder()
                .AddUserSecrets<KODO129StepDefinitions>()
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        [Given(@"a competition I participated in has ended")]
        public void GivenACompetitionIParticipatedInHasEnded()
        {
            // Navigate to compete page and verify there's a completed competition
            _competePage.GoTo();

            // Wait for page to load
            var wait = new WebDriverWait(_webDriver, TimeSpan.FromSeconds(10));
            wait.Until(driver => driver.FindElements(By.ClassName("competitions-section")).Count > 0);

            // Verify there is at least one completed competition
            _competePage.HasCompletedCompetitions().Should().BeTrue("Test requires at least one completed competition in the database");
        }

        [Given(@"a majority of participants have voted to play again in the competition I participated in")]
        public void GivenAMajorityOfParticipantsHaveVotedToPlayAgainInTheCompetitionIParticipatedIn()
        {
            // Navigate to compete page
            _competePage.GoTo();

            // Wait for page to load
            var wait = new WebDriverWait(_webDriver, TimeSpan.FromSeconds(10));
            wait.Until(driver => driver.FindElements(By.ClassName("competitions-section")).Count > 0);

            // This step assumes the database is seeded with a competition where majority voted to play again
            // The test data should have HasMajorityVotedToPlayAgain = true for the competition
        }

        [Given(@"a game has been nominated for the next competition")]
        public void GivenAGameHasBeenNominatedForTheNextCompetition()
        {
            // This step assumes the database is seeded with at least one nominated game
            // The test data should have NominatedGames populated in the ViewModel
        }

        [Given(@"participants have nominated and voted on games for the next competition I participated in")]
        public void GivenParticipantsHaveNominatedAndVotedOnGamesForTheNextCompetitionIParticipatedIn()
        {
            // Navigate to compete page
            _competePage.GoTo();

            // Wait for page to load
            var wait = new WebDriverWait(_webDriver, TimeSpan.FromSeconds(10));
            wait.Until(driver => driver.FindElements(By.ClassName("competitions-section")).Count > 0);

            // This step assumes the database is seeded with:
            // 1. A completed competition
            // 2. Majority voted to play again
            // 3. Games nominated and voted on
            // 4. A winning game determined
        }

        [When(@"I visit the competition details page")]
        public void WhenIVisitTheCompetitionDetailsPage()
        {
            // Click on a completed competition to go to its details page
            _competePage.ClickFirstCompletedCompetition();

            // Wait for details page to load
            _competePage.WaitForDetailsPageLoad();
        }

        [Then(@"I should see an option to vote on whether to participate in another competition")]
        public void ThenIShouldSeeAnOptionToVoteOnWhetherToParticipateInAnotherCompetition()
        {
            // Wait for the page to fully load and check for vote again button
            var wait = new WebDriverWait(_webDriver, TimeSpan.FromSeconds(10));

            try
            {
                var voteAgainDiv = wait.Until(driver => driver.FindElement(By.Id("voteAgainDiv")));
                voteAgainDiv.Should().NotBeNull("Vote again section should be visible on ended competition");

                var voteAgainButton = _competePage.GetVoteAgainButton();
                voteAgainButton.Should().NotBeNull("Vote again button should be visible on ended competition");
                voteAgainButton.Displayed.Should().BeTrue("Vote again button should be displayed");
            }
            catch (WebDriverTimeoutException)
            {
                Assert.Fail("Vote again button was not found on the competition details page. Ensure the competition has ended.");
            }
        }

        [Then(@"I should see an option to nominate a game for the next competition")]
        public void ThenIShouldSeeAnOptionToNominateAGameForTheNextCompetition()
        {
            // Wait for the page to fully load and check for nominate game button
            var wait = new WebDriverWait(_webDriver, TimeSpan.FromSeconds(10));

            try
            {
                var nominateGameDiv = wait.Until(driver => driver.FindElement(By.Id("nominateGameDiv")));
                nominateGameDiv.Should().NotBeNull("Nominate game section should be visible when majority voted to play again");

                var nominateGameButton = _competePage.GetNominateGameButton();
                nominateGameButton.Should().NotBeNull("Nominate game button should be visible when majority voted to play again");
                nominateGameButton.Displayed.Should().BeTrue("Nominate game button should be displayed");
            }
            catch (WebDriverTimeoutException)
            {
                Assert.Fail("Nominate game button was not found. Ensure majority of participants have voted to play again.");
            }
        }

        [Then(@"I should see an option to vote on the nominated games for the next competition")]
        public void ThenIShouldSeeAnOptionToVoteOnTheNominatedGamesForTheNextCompetition()
        {
            // Wait for the page to fully load and check for voting section
            var wait = new WebDriverWait(_webDriver, TimeSpan.FromSeconds(10));

            try
            {
                var votingDiv = wait.Until(driver => driver.FindElement(By.Id("nominatedGamesVotingDiv")));
                votingDiv.Should().NotBeNull("Nominated games voting section should be visible when games have been nominated");

                var nominatedGamesVotingDiv = _competePage.GetNominatedGamesVotingDiv();
                nominatedGamesVotingDiv.Should().NotBeNull("Voting section should be visible when games have been nominated");
                nominatedGamesVotingDiv.Displayed.Should().BeTrue("Voting section should be displayed");

                // Verify there are vote buttons for nominated games
                var voteButtons = _competePage.VoteForGameButtons;
                voteButtons.Count.Should().BeGreaterThan(0, "There should be at least one vote button for nominated games");
            }
            catch (WebDriverTimeoutException)
            {
                Assert.Fail("Nominated games voting section was not found. Ensure games have been nominated for the next competition.");
            }
        }

        [Then(@"the game with the most votes should be selected as the focus of the next competition")]
        public void ThenTheGameWithTheMostVotesShouldBeSelectedAsTheFocusOfTheNextCompetition()
        {
            // Wait for the page to fully load and check for winning game section
            var wait = new WebDriverWait(_webDriver, TimeSpan.FromSeconds(10));

            try
            {
                var winningGameDiv = wait.Until(driver => driver.FindElement(By.Id("winningGameDiv")));
                winningGameDiv.Should().NotBeNull("Winning game section should be visible when voting is complete");

                var winningGameSection = _competePage.GetWinningGameDiv();
                winningGameSection.Should().NotBeNull("Winning game section should be visible");
                winningGameSection.Displayed.Should().BeTrue("Winning game section should be displayed");

                // Verify the winning game name is displayed
                var winningGameName = _competePage.GetWinningGameName();
                winningGameName.Should().NotBeNullOrEmpty("Winning game name should be displayed");
            }
            catch (WebDriverTimeoutException)
            {
                Assert.Fail("Winning game section was not found. Ensure participants have voted on nominated games.");
            }
        }
    }
}
