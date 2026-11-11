using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using BDD_Tests.Shared;
using System.Collections.ObjectModel;

namespace BDD_Tests.PageObjects
{
    public class CompetePageObject : PageObject
    {
        private WebDriverWait _wait;

        public CompetePageObject(IWebDriver webDriver) : base(webDriver)
        {
            // using a named page (in Common.cs)
            _pageName = "Compete";
            _wait = new WebDriverWait(webDriver, TimeSpan.FromSeconds(10));
        }

        public IWebElement RegisterButton => _webDriver.FindElement(By.Id("register-link"));
        public IWebElement NavBarHelloLink => _webDriver.FindElement(By.CssSelector("a[href=\"/Identity/Account/Manage\"]"));
        public IWebElement NavBarCompeteLink => _webDriver.FindElement(By.Id("navCompete"));
        public IWebElement CompeteCreateLink => _webDriver.FindElement(By.Id("compCreateLink"));

        // Competition cards on index page - both running and complete sections
        public ReadOnlyCollection<IWebElement> CompetitionCards => _webDriver.FindElements(By.ClassName("competitionCard"));
        public ReadOnlyCollection<IWebElement> CompetitionDivs => _webDriver.FindElements(By.ClassName("competitionDiv"));

        // Competition section elements for finding completed competitions
        public ReadOnlyCollection<IWebElement> CompetitionsSections => _webDriver.FindElements(By.ClassName("competitions-section"));

        // Competition details page elements
        public IWebElement VoteAgainButton => FindElementSafely(By.Id("voteAgainBtn"));
        public IWebElement VoteAgainDiv => FindElementSafely(By.Id("voteAgainDiv"));
        public IWebElement CompDeleteButton => FindElementSafely(By.Id("compDeleteBtn"));
        public IWebElement CompetitionGameImage => FindElementSafely(By.ClassName("gameImage"));
        public IWebElement CompetitorsRow => FindElementSafely(By.ClassName("competitors-row"));
        public IWebElement LeaderboardSection => FindElementSafely(By.Id("CompletionHistory"));

        // Nomination and voting elements (for KODO263 feature)
        public IWebElement NominateGameButton => FindElementSafely(By.Id("nominateGameBtn"));
        public IWebElement NominateGameDiv => FindElementSafely(By.Id("nominateGameDiv"));
        public IWebElement NominatedGamesVotingDiv => FindElementSafely(By.Id("nominatedGamesVotingDiv"));
        public IWebElement WinningGameDiv => FindElementSafely(By.Id("winningGameDiv"));
        public IWebElement WinningGameName => FindElementSafely(By.Id("winningGameName"));
        public ReadOnlyCollection<IWebElement> VoteForGameButtons => _webDriver.FindElements(By.ClassName("voteForGameBtn"));
        public ReadOnlyCollection<IWebElement> NominatedGameItems => _webDriver.FindElements(By.ClassName("nominated-game-item"));

        public IWebElement FindElementSafely(By by)
        {
            try
            {
                return _webDriver.FindElement(by);
            }
            catch (NoSuchElementException)
            {
                return null;
            }
        }

        /// <summary>
        /// Waits for an element to be present with a timeout
        /// </summary>
        public IWebElement WaitForElement(By by, int timeoutSeconds = 10)
        {
            try
            {
                var wait = new WebDriverWait(_webDriver, TimeSpan.FromSeconds(timeoutSeconds));
                return wait.Until(driver => driver.FindElement(by));
            }
            catch (WebDriverTimeoutException)
            {
                return null;
            }
        }

        public string NavbarWelcomeText()
        {
            return NavBarHelloLink.Text;
        }

        public void ClickNavBarCompeteLink()
        {
            NavBarCompeteLink.Click();
        }

        public void ClickCompeteCreateLink()
        {
            CompeteCreateLink.Click();
        }

        /// <summary>
        /// Clicks the first competition card to navigate to its details page
        /// </summary>
        public void ClickFirstCompetition()
        {
            if (CompetitionDivs.Count > 0)
            {
                CompetitionDivs[0].Click();
            }
        }

        /// <summary>
        /// Clicks the first completed competition card to navigate to its details page.
        /// Completed competitions are in the second competitions-section (after "Complete" heading).
        /// </summary>
        public void ClickFirstCompletedCompetition()
        {
            // Find all competition sections - the second one contains completed competitions
            var sections = CompetitionsSections;
            if (sections.Count >= 2)
            {
                // Get competition divs within the "Complete" section
                var completedCompetitions = sections[1].FindElements(By.ClassName("competitionDiv"));
                if (completedCompetitions.Count > 0)
                {
                    completedCompetitions[0].Click();
                    return;
                }
            }
            // Fallback to first competition if no completed ones found
            ClickFirstCompetition();
        }

        /// <summary>
        /// Checks if there are any completed competitions on the index page
        /// </summary>
        public bool HasCompletedCompetitions()
        {
            var sections = CompetitionsSections;
            if (sections.Count >= 2)
            {
                var completedCompetitions = sections[1].FindElements(By.ClassName("competitionDiv"));
                return completedCompetitions.Count > 0;
            }
            return false;
        }

        /// <summary>
        /// Gets the vote again button on the competition details page
        /// </summary>
        public IWebElement GetVoteAgainButton()
        {
            return VoteAgainButton;
        }

        /// <summary>
        /// Gets the nominate game button on the competition details page
        /// </summary>
        public IWebElement GetNominateGameButton()
        {
            return NominateGameButton;
        }

        /// <summary>
        /// Gets the nominated games voting section
        /// </summary>
        public IWebElement GetNominatedGamesVotingDiv()
        {
            return NominatedGamesVotingDiv;
        }

        /// <summary>
        /// Gets the winning game div that shows the selected game for next competition
        /// </summary>
        public IWebElement GetWinningGameDiv()
        {
            return WinningGameDiv;
        }

        /// <summary>
        /// Gets the winning game name
        /// </summary>
        public string GetWinningGameName()
        {
            return WinningGameName?.Text;
        }

        /// <summary>
        /// Checks if we are on the competition details page
        /// </summary>
        public bool IsOnDetailsPage()
        {
            return CompetitionGameImage != null || CompetitorsRow != null;
        }

        /// <summary>
        /// Waits for the details page to load
        /// </summary>
        public bool WaitForDetailsPageLoad(int timeoutSeconds = 10)
        {
            try
            {
                var wait = new WebDriverWait(_webDriver, TimeSpan.FromSeconds(timeoutSeconds));
                wait.Until(driver =>
                    driver.FindElement(By.ClassName("gameImage")) != null ||
                    driver.FindElement(By.ClassName("competitors-row")) != null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Logout()
        {
            IWebElement navbarLogoutButton = _webDriver.FindElement(By.Id("logout-button"));
            navbarLogoutButton.Click();
        }
    }
}
