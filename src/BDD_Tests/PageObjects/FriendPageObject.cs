using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using BDD_Tests.Shared;
using System.Collections.ObjectModel;

namespace BDD_Tests.PageObjects
{
    public class FriendPageObject : PageObject
    {
        private WebDriverWait _wait;

        public FriendPageObject(IWebDriver webDriver) : base(webDriver)
        {
            // using a named page (in Common.cs)
            _pageName = "Friend";
            _wait = new WebDriverWait(webDriver, TimeSpan.FromSeconds(10));
        }

        public IWebElement RegisterButton => _webDriver.FindElement(By.Id("register-link"));
        public IWebElement NavBarHelloLink => _webDriver.FindElement(By.CssSelector("a[href=\"/Identity/Account/Manage\"]"));
        public IWebElement FriendUserName => _wait.Until(ExpectedConditions.ElementIsVisible(By.Id("friendUsername")));

        public ReadOnlyCollection<IWebElement> GetSharedGames()
        {
            // Wait for the page to load and check for shared games
            Thread.Sleep(1000);
            return _webDriver.FindElements(By.ClassName("friendDiv"));
        }


        public IWebElement GetFriendUsername()
        {
            return FriendUserName;
        }




        public void Logout()
        {
            IWebElement navbarLogoutButton = _webDriver.FindElement(By.Id("logout-button"));
            navbarLogoutButton.Click();
        }
    }
}
