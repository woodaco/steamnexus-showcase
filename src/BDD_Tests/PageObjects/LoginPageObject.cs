using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using System.Collections.ObjectModel;

namespace BDD_Tests.PageObjects
{
    public class LoginPageObject : PageObject
    {
        public LoginPageObject(IWebDriver webDriver) : base(webDriver)
        {
            _pageName = "Login";
            WaitForLoginForm();
        }

        /// <summary>
        /// Right after a deploy the App Service container is still starting, and the first
        /// request can land on Azure's "starting up" page or a cold-database timeout. Reload
        /// until the real login form is there (up to two minutes) instead of failing at once.
        /// </summary>
        private void WaitForLoginForm()
        {
            var url = _webDriver.Url;
            var deadline = DateTime.UtcNow.AddSeconds(120);
            while (DateTime.UtcNow < deadline)
            {
                if (FindElementSafely(By.Id("Input_Email")) != null) return;
                Thread.Sleep(5000);
                try { _webDriver.Navigate().GoToUrl(url); } catch (WebDriverException) { }
            }
        }

        private IWebElement FindElementSafely(By by)
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

        public IWebElement EmailInput => FindElementSafely(By.Id("Input_Email"));
        public IWebElement PasswordInput => FindElementSafely(By.Id("Input_Password"));
        public IWebElement RememberMeCheck => FindElementSafely(By.Id("Input_RememberMe"));
        public IWebElement SubmitButton => FindElementSafely(By.Id("login-submit"));

        public void EnterEmail(string email)
        {
            if (EmailInput != null)
            {
                EmailInput.Clear();
                EmailInput.SendKeys(email);
            }
            else
            {
                throw new NoSuchElementException("Email input field not found");
            }
        }

        public void EnterPassword(string password)
        {
            if (PasswordInput != null)
            {
                PasswordInput.Clear();
                PasswordInput.SendKeys(password);
            }
            else
            {
                throw new NoSuchElementException("Password input field not found");
            }
        }

        public void SetRememberMe(bool rememberMe)
        {
            if (RememberMeCheck != null)
            {
                if (rememberMe)
                {
                    if (!RememberMeCheck.Selected)
                    {
                        RememberMeCheck.Click();
                    }
                }
                else
                {
                    if (RememberMeCheck.Selected)
                    {
                        RememberMeCheck.Click();
                    }
                }
            }
            else
            {
                throw new NoSuchElementException("Remember me check box not found");
            }
        }

        public void Login()
        {
            // Wait for the SPA to render the login form before interacting.
            var readyWait = new WebDriverWait(_webDriver, TimeSpan.FromSeconds(15));
            readyWait.Until(d => d.FindElements(By.Id("login-submit")).Count > 0);

            IWebElement button;
            try
            {
                button = _webDriver.FindElement(By.Id("login-submit"));
            }
            catch (NoSuchElementException)
            {
                throw new NoSuchElementException("Submit button not found");
            }

            try
            {
                button.Click();
            }
            catch (OpenQA.Selenium.ElementClickInterceptedException)
            {
                var js = _webDriver as OpenQA.Selenium.IJavaScriptExecutor;
                js.ExecuteScript("arguments[0].click();", button);
            }

            // Wait for navigation away from login page after click.
            // Also accept validation-summary-errors on-page (server returned login page
            // with error, e.g. email not confirmed).
            // Extract path from URL to handle scheme/host/port/query variations.
            var wait = new WebDriverWait(_webDriver, TimeSpan.FromSeconds(45));
            wait.Until(d => {
                var uri = new Uri(d.Url);
                return !uri.LocalPath.ToLower().Equals("/identity/account/login")
                    || d.FindElements(By.CssSelector(".validation-summary-errors")).Count > 0;
            });
        }

        public bool HasLoginErrors()
        {
            ReadOnlyCollection<IWebElement> elements = _webDriver.FindElements(By.CssSelector(".validation-summary-errors"));
            return elements.Count > 0;
        }
    }
}
