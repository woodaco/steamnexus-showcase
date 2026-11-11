using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BDD_Tests.Shared
{
    // Sitewide definitions and useful methods
    public class Common
    {
        // Use environment variable for CI, fallback to localhost for local dev
        public static readonly string BaseUrl = Environment.GetEnvironmentVariable("BDD_BASE_URL")
            ?? "https://localhost:7123";
        

        // File to store browser cookies in
        public const string CookieFile = "..\\..\\..\\..\\..\\..\\..\\StandupsCookies.txt";

        // Page names that everyone should use
        // A handy way to look these up
        public static readonly Dictionary<string, string> Paths = new()
        {
            { "Home" , "/" },
            { "Login", "/Identity/Account/Login" },
            { "Compete", "/Compete" },
            { "Compete Create", "/Compete/Create" },
            { "Friends", "/Home/Friends" },
            { "Dashboard", "/Home/Dashboard"},
            { "Library","/Library/Index" },
            { "External Logins", "/Identity/Account/Manage/ExternalLogins" },
            { "Profile", "/Identity/Account/Manage" }
        };

        public static string PathFor(string pathName) => Paths[pathName];
        public static string UrlFor(string pathName) => BaseUrl + Paths[pathName];
    }
}
