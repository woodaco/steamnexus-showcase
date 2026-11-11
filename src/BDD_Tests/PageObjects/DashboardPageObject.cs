using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using BDD_Tests.PageObjects;
using SpecFlow.Actions.Selenium;

namespace BDD_Tests.PageObjects
{
    public class DashboardPageObject : PageObject
    {
        private WebDriverWait _wait;

        public DashboardPageObject(IWebDriver webDriver) : base(webDriver)
        {
            // using a named page (in Common.cs)
            _pageName = "Dashboard";
            _wait = new WebDriverWait(webDriver, TimeSpan.FromSeconds(10));
        }

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


        public IWebElement ToDoButton => _webDriver.FindElement(By.Id("toDoButton"));

        public IWebElement ToDoListDiv => _webDriver.FindElement(By.Id("toDoCollapse"));

        public IWebElement ProfileImage => _webDriver.FindElement(By.ClassName("user-avatar"));

        public IWebElement SteamLevel => _webDriver.FindElement(By.Id("divSteamNameLevel"));

        public IWebElement SteamUserName => _webDriver.FindElement(By.Id("userName"));

        public IWebElement RecentGamesCarousel => FindElementSafely(By.Id("recentGamesCarousel"));

        public IWebElement FollowedGamesCarousel => FindElementSafely(By.Id("followedGamesCarousel"));


        public IWebElement NavBarHelloLink => _webDriver.FindElement(By.CssSelector("a[href=\"/Identity/Account/Manage\"]"));

        public IReadOnlyCollection<IWebElement> Badges =>
            _webDriver.FindElements(By.ClassName("badge-button-img"));

        public IReadOnlyCollection<IWebElement> BadgeDescriptions =>
            _webDriver.FindElements(By.ClassName("badge-description"));

        public string NavbarWelcomeText()
        {
            return NavBarHelloLink.Text;
        }

        public bool ProfileImageVisible()
        {
            return (ProfileImage != null);
        }
        public bool UsernameAndLevelVisible()
        {
            return (SteamUserName != null && SteamLevel != null);
        }

        public bool RecentGamesIsVisible()
        {
            try
            {
                // Wait a moment for page to fully load
                Thread.Sleep(1000);
                var element = _wait.Until(ExpectedConditions.ElementExists(By.Id("recentGamesCarousel")));
                return element != null && element.Displayed;
            }
            catch (WebDriverTimeoutException)
            {
                return false;
            }
        }

        public bool FollowedGamesIsVisible()
        {
            try
            {
                // Wait a moment for page to fully load
                Thread.Sleep(1000);
                var element = _wait.Until(ExpectedConditions.ElementExists(By.Id("followedGamesCarousel")));
                return element != null && element.Displayed;
            }
            catch (WebDriverTimeoutException)
            {
                return false;
            }
        }


        public void Logout()
        {
            IWebElement navbarLogoutButton = _webDriver.FindElement(By.Id("logout-button"));
            navbarLogoutButton.Click();
        }


 
        public bool IsBadgePresent()
        {
            return Badges.Count > 0;
        }

        /// <summary>
        /// Gets the badge image element by its alt text (badge name).
        /// </summary>
        public IWebElement GetBadgeByName(string badgeName)
        {
            try
            {
                return _wait.Until(driver => driver.FindElement(By.XPath($"//img[@alt='{badgeName}']")));
            }
            catch (WebDriverTimeoutException)
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the badge button element by the badge name.
        /// The button contains the badge image and triggers the collapse to show description.
        /// </summary>
        public IWebElement GetBadgeButtonByName(string badgeName)
        {
            try
            {
                // Find the button that contains the badge image with the specified alt text
                return _wait.Until(driver => driver.FindElement(By.XPath($"//button[contains(@class, 'badge-button-img') and .//img[@alt='{badgeName}']]")));
            }
            catch (WebDriverTimeoutException)
            {
                // Fallback: find the parent button of the badge image
                var badgeImg = GetBadgeByName(badgeName);
                if (badgeImg != null)
                {
                    return badgeImg.FindElement(By.XPath("./ancestor::button"));
                }
                return null;
            }
        }

        public void ClickBadge(IWebElement badge)
        {
            badge.Click();
        }

        public string GetBadgeDescription(IWebElement badgeDescription)
        {
            return badgeDescription.Text;
        }

        /// <summary>
        /// Clicks on the badge button to show its description.
        /// </summary>
        public void GoToBadgeDetails(string badgeName)
        {
            var badgeButton = GetBadgeButtonByName(badgeName);
            if (badgeButton != null)
            {
                badgeButton.Click();
                // Wait a moment for the collapse animation
                Thread.Sleep(500);
            }
        }

        /// <summary>
        /// Gets the visible badge description element after clicking a badge.
        /// Returns the description element if it's visible, null otherwise.
        /// </summary>
        public IWebElement GetVisibleBadgeDescription()
        {
            try
            {
                // Find badge descriptions that are visible (have 'show' class from Bootstrap collapse)
                var descriptions = _webDriver.FindElements(By.CssSelector(".badge-description.show"));
                return descriptions.FirstOrDefault(d => d.Displayed);
            }
            catch (NoSuchElementException)
            {
                return null;
            }
        }

        /// <summary>
        /// Checks if any badge description is currently visible.
        /// </summary>
        public bool IsBadgeDescriptionVisible()
        {
            var description = GetVisibleBadgeDescription();
            return description != null && description.Displayed;
        }
    }
}


