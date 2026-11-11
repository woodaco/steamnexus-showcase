using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using BDD_Tests.Shared;
using System.Collections.ObjectModel;

namespace BDD_Tests.PageObjects
{
    public class LibraryPageObject : PageObject
    {
        private WebDriverWait _wait;

        public LibraryPageObject(IWebDriver webDriver) : base(webDriver)
        {
            // using a named page (in Common.cs)
            _pageName = "Library";
            _wait = new WebDriverWait(webDriver, TimeSpan.FromSeconds(10));
        }

        public IWebElement RegisterButton => _webDriver.FindElement(By.Id("register-link"));
        public IWebElement NavBarHelloLink => _webDriver.FindElement(By.CssSelector("a[href=\"/Identity/Account/Manage\"]"));
        public IWebElement SteamLinkButton => _webDriver.FindElement(By.Id("link-login-button-Steam"));
        public IWebElement SteamAvatarImg => _webDriver.FindElement(By.CssSelector(".library-avatar img"));
        public ReadOnlyCollection<IWebElement> FollowGamesButtons => _webDriver.FindElements(By.ClassName("follow-btn"));

        public IWebElement LinkingMessage => _webDriver.FindElement(By.Id("link-message"));
        public IWebElement GameLibrary => _webDriver.FindElement(By.Id("game-Library"));
        public IWebElement HiddenModal => _webDriver.FindElement(By.Id("hidden-game-modal"));
        public IWebElement RefreshButton => _webDriver.FindElement(By.CssSelector("button[title='Refresh Library']"));
        public IWebElement ShowHiddenModalButton => _webDriver.FindElement(By.CssSelector("button[title='Show Hidden Games']"));
        public IWebElement EmptyModalMessage => HiddenModal.FindElement(By.Id("hidden-modal-no-game-message"));
        public IWebElement HiddenModalCloseButton => HiddenModal.FindElement(By.ClassName("btn-close-white"));

        public void SteamLinkButtonClick()
        {
            SteamLinkButton.Click();
        }

        public void FollowFirstGame()
        {
            FollowGamesButtons.FirstOrDefault();

            if (FollowGamesButtons.Count > 0)
            {
                FollowGamesButtons.FirstOrDefault().Click();
            }
        }

        public bool SteamAvatarImgVisible()
        {
            return SteamAvatarImg != null;
        }
        public void Logout()
        {
            IWebElement navbarLogoutButton = _webDriver.FindElement(By.Id("logout-button"));
            navbarLogoutButton.Click();
        }

        public bool GetLinkingMessage()
        {
            try
            {
                return LinkingMessage != null && LinkingMessage.Displayed;
            }
            catch (NoSuchElementException)
            {
                return false;
            }
        }

        public void Refresh()
        {
            RefreshButton.Click();
            // Wait for the page to reload
            Thread.Sleep(2000);
            // Wait for the game library to be visible again
            _wait.Until(ExpectedConditions.ElementExists(By.Id("game-Library")));
        }

        public bool ContainsGame(string gameName)
        {
            try
            {
                var gameContainer = FindGameContainer(gameName);
                return gameContainer != null && gameContainer.Displayed;
            }
            catch (NoSuchElementException)
            {
                return false;
            }
            catch (WebDriverTimeoutException)
            {
                return false;
            }
        }

        public bool DoesNotContainGame(string gameName)
        {
            try
            {
                // Wait a bit for any page updates to settle
                Thread.Sleep(500);

                // Check if element exists
                var gameContainers = _webDriver.FindElements(By.CssSelector("#game-Library .gameContainer"));
                foreach (var container in gameContainers)
                {
                    var id = container.GetAttribute("id");
                    if (id != null && id.Equals(gameName, StringComparison.OrdinalIgnoreCase))
                    {
                        return false; // Game is still visible
                    }
                }
                return true; // Game not found, so it's hidden
            }
            catch (NoSuchElementException)
            {
                return true;
            }
        }

        public void ShowHiddenModal()
        {
            ShowHiddenModalButton.Click();
            // Wait for the modal to be visible
            _wait.Until(ExpectedConditions.ElementIsVisible(By.Id("hidden-game-modal")));
        }

        public bool GetEmptyModalMessage()
        {
            try
            {
                var element = _wait.Until(ExpectedConditions.ElementIsVisible(By.Id("hidden-modal-no-game-message")));
                return element != null && element.Displayed;
            }
            catch (WebDriverTimeoutException)
            {
                return false;
            }
        }

        /// <summary>
        /// Finds a game container element by game name. The game name is used as the ID.
        /// </summary>
        private IWebElement FindGameContainer(string gameName)
        {
            // Wait for the game library to be loaded
            _wait.Until(ExpectedConditions.ElementExists(By.Id("game-Library")));

            // Try to find the game by ID (exact match)
            try
            {
                return _wait.Until(ExpectedConditions.ElementExists(By.Id(gameName)));
            }
            catch (WebDriverTimeoutException)
            {
                // If not found by exact ID, try searching through game containers
                var gameContainers = _webDriver.FindElements(By.CssSelector("#game-Library .gameContainer"));
                foreach (var container in gameContainers)
                {
                    var id = container.GetAttribute("id");
                    if (id != null && id.Contains(gameName))
                    {
                        return container;
                    }
                }
                throw new NoSuchElementException($"Could not find game container for '{gameName}'");
            }
        }

        public bool FindHideButtonForGame(string gameName)
        {
            try
            {
                var gameContainer = FindGameContainer(gameName);
                var hideButton = gameContainer.FindElement(By.ClassName("hide-btn"));
                return hideButton != null && hideButton.Displayed;
            }
            catch (NoSuchElementException)
            {
                return false;
            }
        }

        public void HideGame(string gameName)
        {
            var gameContainer = FindGameContainer(gameName);
            var hideButton = gameContainer.FindElement(By.ClassName("hide-btn"));
            hideButton.Click();
            // Wait for the game to be hidden (give time for the AJAX call)
            Thread.Sleep(1000);
        }

        public void Unhide(string gameName)
        {
            ShowHiddenModal();
            // Wait a moment for modal content to load
            Thread.Sleep(500);

            // Find the game in the hidden modal
            try
            {
                var gameContainer = HiddenModal.FindElement(By.Id(gameName));
                var unhideButton = gameContainer.FindElement(By.ClassName("unhide-btn"));
                unhideButton.Click();
                // Wait for the unhide action to complete
                Thread.Sleep(1000);
            }
            catch (NoSuchElementException)
            {
                // Try finding by partial match in case of special characters
                var gameContainers = HiddenModal.FindElements(By.ClassName("gameContainer"));
                foreach (var container in gameContainers)
                {
                    var id = container.GetAttribute("id");
                    if (id != null && id.Contains(gameName))
                    {
                        var unhideButton = container.FindElement(By.ClassName("unhide-btn"));
                        unhideButton.Click();
                        Thread.Sleep(1000);
                        break;
                    }
                }
            }

            HiddenModalCloseButton.Click();
        }
    }
}
