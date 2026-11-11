using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Safari;
using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BDD_Tests.Drivers
{
    public class BrowserDriver : IDisposable
    {
        private readonly Lazy<IWebDriver> _currentWebDriverLazy;
        private bool _isDisposed = false;

        public BrowserDriver()
        {
            _currentWebDriverLazy = new Lazy<IWebDriver>(CreateWebDriver);
        }

        public IWebDriver Current => _currentWebDriverLazy.Value;

        // Expose WebDriver instance as a public property
        public IWebDriver WebDriver => Current;

        /// <summary>
        /// Returns true if the WebDriver lazy instance has been initialized.
        /// </summary>
        public bool IsWebDriverCreated()
        {
            return _currentWebDriverLazy.IsValueCreated;
        }

        private IWebDriver CreateWebDriver()
        {
            // Portable headless Chrome (Selenium Manager resolves the driver). This replaces a
            // hardcoded Windows-only Firefox path that broke every non-Windows / CI machine.
            // Set BDD_HEADLESS=false to watch the browser locally.
            var headless = Environment.GetEnvironmentVariable("BDD_HEADLESS") != "false";

            var chromeOptions = new ChromeOptions();
            if (headless) chromeOptions.AddArgument("--headless=new");
            chromeOptions.AddArgument("--no-sandbox");
            chromeOptions.AddArgument("--disable-dev-shm-usage");
            chromeOptions.AddArgument("--disable-gpu");
            chromeOptions.AddArgument("--window-size=1920,1080");
            chromeOptions.AddArgument("--incognito");
            chromeOptions.AcceptInsecureCertificates = true;

            // Allow pointing at a specific Chrome/Chromium binary (e.g., a CI image) without code changes.
            var chromeBinary = Environment.GetEnvironmentVariable("CHROME_BIN");
            if (!string.IsNullOrEmpty(chromeBinary)) chromeOptions.BinaryLocation = chromeBinary;

            return new ChromeDriver(chromeOptions);
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            if (_currentWebDriverLazy.IsValueCreated)
            {
                WebDriver.Quit();
            }

            _isDisposed = true;
        }

    }
}