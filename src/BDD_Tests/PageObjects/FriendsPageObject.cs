using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using BDD_Tests.Shared;
using System.Collections.ObjectModel;
using BDD_Tests.Drivers;
using SpecFlow.Actions.Selenium;

namespace BDD_Tests.PageObjects
{
    public class FriendsPageObject : PageObject
    {
        private WebDriverWait _wait;

        public FriendsPageObject(IWebDriver webDriver) : base(webDriver)
        {
            // using a named page (in Common.cs)
            _pageName = "Friends";
            _wait = new WebDriverWait(webDriver, TimeSpan.FromSeconds(10));
        }

        public IWebElement Invite => _wait.Until(ExpectedConditions.ElementToBeClickable(By.Id("inv-friend-i")));
        public IWebElement InviteModal => _webDriver.FindElement(By.Id("sendInviteModal"));
        public IWebElement EmailBox => _wait.Until(ExpectedConditions.ElementToBeClickable(By.Id("email-input")));
        public IWebElement SendBtn => _wait.Until(ExpectedConditions.ElementToBeClickable(By.Id("send-inv")));
        public IWebElement InviteResult => _webDriver.FindElement(By.Id("email-error"));
        public IWebElement FriendSearch => _wait.Until(ExpectedConditions.ElementToBeClickable(By.Id("search-input")));

        /// <summary>
        /// Finds a friend card that contains "Steve" in its id attribute.
        /// Friend cards have id format: "{SteamName} {Nickname}" or just "{SteamName}"
        /// </summary>
        public IWebElement FriendNamedSteve => _wait.Until(driver =>
        {
            var cards = driver.FindElements(By.ClassName("friend-card"));
            foreach (var card in cards)
            {
                var cardId = card.GetAttribute("id");
                if (cardId != null && cardId.Contains("Steve"))
                {
                    return card;
                }
            }
            return null;
        });

        /// <summary>
        /// Finds a friend card that contains the specified name in its id attribute.
        /// Friend cards have id format: "{SteamName} {Nickname}" or just "{SteamName}"
        /// </summary>
        public IWebElement GetFriendCardContainingName(string friendName)
        {
            return _wait.Until(driver =>
            {
                var cards = driver.FindElements(By.ClassName("friend-card"));
                foreach (var card in cards)
                {
                    var cardId = card.GetAttribute("id");
                    if (cardId != null && cardId.Contains(friendName))
                    {
                        return card;
                    }
                }
                return null;
            });
        }

        public List<string> GetAllFriendSteamIds()
        {

            var allFriendElements = _webDriver.FindElements(By.ClassName("friend-persona"));

            var allFriendIds = new List<string>();

            foreach (var friendElement in allFriendElements)
            {
                var friendId = friendElement.GetAttribute("id");
                if (!string.IsNullOrEmpty(friendId))
                {
                    friendId = friendId.Replace("persona-", "");
                    allFriendIds.Add(friendId);
                }
            }

            return allFriendIds;
        }




        public IWebElement GetFriendCardByName(string friendName)
        {
            // Friend card id format is "{SteamName} {Nickname}" or just "{SteamName}"
            // We need to find a card that starts with or contains the friend name
            return GetFriendCardContainingName(friendName);
        }

        /// <summary>
        /// Navigates to a friend's shared games page by clicking the compare-games button
        /// </summary>
        /// <param name="friendName">The friend's Steam name or nickname</param>
        public void GoToFriendSharedGames(string friendName)
        {
            // Find the friend card - the ID is the friend's SteamName (possibly with Nickname appended)
            // We need to find a card that contains the friend name
            var friendCards = _webDriver.FindElements(By.ClassName("friend-card"));
            foreach (var card in friendCards)
            {
                var cardId = card.GetAttribute("id");
                if (cardId != null && cardId.Contains(friendName))
                {
                    // Find the compare-games button inside this card and click it
                    var compareButton = card.FindElement(By.ClassName("compare-games"));
                    compareButton.Click();
                    return;
                }
            }
            throw new NoSuchElementException($"Could not find friend card for '{friendName}'");
        }

        public IWebElement NicknameTextBox => _webDriver.FindElement(By.ClassName("friend-name"));

        public IWebElement GetRevertButtonBySteamId(string friendId)
        {
            return _wait.Until(ExpectedConditions.ElementToBeClickable(By.CssSelector($"i[data-revert='{friendId}']")));
        }


        public void ClickName(string friendName)
        {
            // Click on the friend-name element inside the friend card, not the card itself
            IWebElement friendCard = GetFriendCardByName(friendName);
            IWebElement friendNameElement = friendCard.FindElement(By.ClassName("friend-name"));
            friendNameElement.Click();
        }

        /// <summary>
        /// Enters a nickname in an already visible input box (after clicking on the friend name).
        /// This is used when the name-box input is already displayed after clicking the friend name.
        /// </summary>
        public void EnterNicknameInVisibleInputBox(string alias)
        {
            Thread.Sleep(500);
            IWebElement nicknameTextBox = _wait.Until(ExpectedConditions.ElementIsVisible(By.ClassName("name-box")));
            nicknameTextBox.SendKeys(alias);
            Thread.Sleep(500);
            nicknameTextBox.SendKeys(Keys.Enter);
        }

        public void GiveNewName(string friendName, string alias)
        {
            IWebElement friendCard = GetFriendCardByName(friendName);
            IWebElement friendNameElement = friendCard.FindElement(By.ClassName("friend-name"));
            friendNameElement.Click();

            Thread.Sleep(500);
            WebDriverWait wait = new WebDriverWait(_webDriver, TimeSpan.FromSeconds(10));
            IWebElement NicknameTextBox = wait.Until(ExpectedConditions.ElementIsVisible(By.ClassName("name-box")));


            NicknameTextBox.SendKeys(alias);
            Thread.Sleep(500);
            NicknameTextBox.SendKeys(Keys.Enter);
        }



        public string NewName(string friendName)
        {
            // Wait for the name element to be present and get its text
            return _wait.Until(ExpectedConditions.ElementIsVisible(By.Id($"name-{friendName}"))).Text;
        }




        public void Revert(string friendId)
        {
            GetRevertButtonBySteamId(friendId).Click();
        }

        /// <summary>
        /// Checks if the invite modal is currently hidden/not displayed.
        /// The modal is considered hidden when its display style is "none" or it's not visible.
        /// </summary>
        public bool IsInviteModalHidden()
        {
            try
            {
                var modal = _webDriver.FindElement(By.Id("sendInviteModal"));
                // Check if modal has display:none or is not displayed
                var displayStyle = modal.GetCssValue("display");
                var isDisplayed = modal.Displayed;
                return displayStyle == "none" || !isDisplayed;
            }
            catch (NoSuchElementException)
            {
                // Modal element not found, consider it hidden
                return true;
            }
            catch (StaleElementReferenceException)
            {
                // Element is stale, consider it hidden
                return true;
            }
        }

        /// <summary>
        /// Waits for the invite modal to be hidden/closed.
        /// </summary>
        public bool WaitForInviteModalToClose()
        {
            try
            {
                return _wait.Until(driver =>
                {
                    try
                    {
                        var modal = driver.FindElement(By.Id("sendInviteModal"));
                        var displayStyle = modal.GetCssValue("display");
                        // Modal is closed when display is "none"
                        return displayStyle == "none";
                    }
                    catch
                    {
                        return true;
                    }
                });
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
    }
}
