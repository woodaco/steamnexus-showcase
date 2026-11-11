using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using BDD_Tests.Shared;
using System.Collections.ObjectModel;

namespace BDD_Tests.PageObjects
{
    public class ProfilePageObject : PageObject
    {
        public ProfilePageObject(IWebDriver webDriver) : base(webDriver)
        {
            // using a named page (in Common.cs)
            _pageName = "Profile";
        }

        private IWebElement FindElementSafely(By by)
        {
            try
            {
                var wait = new WebDriverWait(_webDriver, TimeSpan.FromSeconds(DefaultWaitInSeconds));
                return wait.Until(driver => driver.FindElement(by));
            }
            catch (WebDriverTimeoutException)
            {
                return null;
            }
            catch (NoSuchElementException)
            {
                return null;
            }
        }

        public IWebElement RegisterButton => _webDriver.FindElement(By.Id("register-link"));
        public IWebElement NavBarHelloLink => _webDriver.FindElement(By.CssSelector("a[href=\"/Identity/Account/Manage\"]"));
        public IWebElement FriendsList => FindElementSafely(By.Id("friendsListDiv"));
        public IWebElement ProfileImage => FindElementSafely(By.Id("divSteamAvatar"));
        public IWebElement UsernameAndLevel => FindElementSafely(By.Id("divSteamNameLevel"));

        public IWebElement SteamLinkPage => FindElementSafely(By.Id("external-login"));


        public bool FriendsListVisible()
        {
            var element = FriendsList;
            return element != null && element.Displayed;
        }

        public bool ProfileImageVisible()
        {
            var element = ProfileImage;
            return element != null && element.Displayed;
        }

        public bool UsernameAndLevelVisible()
        {
            var element = UsernameAndLevel;
            return element != null && element.Displayed;
        }
        public void GoToFriendPage(string friendName)
        {
            string friendId = "$" + friendName;
            IWebElement friendLink = _webDriver.FindElement(By.Id(friendId));
            friendLink.Click();
        }

        public void SteamLinkPageClick()
        {
            SteamLinkPage.Click();
        }

        public void Logout()
        {
            IWebElement navbarLogoutButton = _webDriver.FindElement(By.Id("logout-button"));
            navbarLogoutButton.Click();
        }
    }
}
