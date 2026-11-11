using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using SteamProject.Utilities;

namespace NUnit_Tests.MiddlewareTesting
{
    [TestFixture]
    public class ApiAwareCookieEventsTests
    {
        private static RedirectContext<CookieAuthenticationOptions> MakeContext(string path, string redirectUri)
        {
            var http = new DefaultHttpContext();
            http.Request.Path = path;
            var scheme = new AuthenticationScheme(CookieAuthenticationDefaults.AuthenticationScheme, null, typeof(CookieAuthenticationHandler));
            return new RedirectContext<CookieAuthenticationOptions>(http, scheme, new CookieAuthenticationOptions(), new AuthenticationProperties(), redirectUri);
        }

        [TestCase("/api/Steam/user")]
        [TestCase("/api")]
        [TestCase("/API/messages")]
        public async Task RedirectToLogin_ForApiPath_Returns401WithoutLocation(string path)
        {
            var ctx = MakeContext(path, "/Identity/Account/Login?ReturnUrl=x");

            await ApiAwareCookieEvents.RedirectToLogin(ctx);

            Assert.That(ctx.Response.StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));
            Assert.That(ctx.Response.Headers.Location.ToString(), Is.Empty);
        }

        [Test]
        public async Task RedirectToAccessDenied_ForApiPath_Returns403WithoutLocation()
        {
            var ctx = MakeContext("/api/Badge/1", "/Identity/Account/AccessDenied");

            await ApiAwareCookieEvents.RedirectToAccessDenied(ctx);

            Assert.That(ctx.Response.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
            Assert.That(ctx.Response.Headers.Location.ToString(), Is.Empty);
        }

        [TestCase("/Compete")]
        [TestCase("/apiary")]
        [TestCase("/Home/Dashboard")]
        public async Task RedirectToLogin_ForPagePath_StillRedirects(string path)
        {
            var ctx = MakeContext(path, "/Identity/Account/Login?ReturnUrl=x");

            await ApiAwareCookieEvents.RedirectToLogin(ctx);

            Assert.That(ctx.Response.StatusCode, Is.EqualTo(StatusCodes.Status302Found));
            Assert.That(ctx.Response.Headers.Location.ToString(), Is.EqualTo("/Identity/Account/Login?ReturnUrl=x"));
        }

        [Test]
        public async Task RedirectToAccessDenied_ForPagePath_StillRedirects()
        {
            var ctx = MakeContext("/Admin", "/Identity/Account/AccessDenied");

            await ApiAwareCookieEvents.RedirectToAccessDenied(ctx);

            Assert.That(ctx.Response.StatusCode, Is.EqualTo(StatusCodes.Status302Found));
            Assert.That(ctx.Response.Headers.Location.ToString(), Is.EqualTo("/Identity/Account/AccessDenied"));
        }
    }
}
