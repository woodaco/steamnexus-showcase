using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using SteamProject.Controllers;
using SteamProject.DAL.Abstract;
using SteamProject.Services;

namespace NUnit_Tests.ControllerTesting
{
    [TestFixture]
    public class HomeControllerTests
    {
        private HomeController _controller = null!;

        [SetUp]
        public void SetUp()
        {
            var userManager = new Mock<UserManager<IdentityUser>>(
                new Mock<IUserStore<IdentityUser>>().Object,
                Options.Create(new IdentityOptions()),
                null, null, null, null, null, null, null);

            _controller = new HomeController(
                userManager.Object,
                new Mock<IUserRepository>().Object,
                new Mock<IGameRepository>().Object,
                new Mock<IUserGameInfoRepository>().Object,
                new Mock<ISteamService>().Object,
                new Mock<IFriendRepository>().Object,
                new Mock<IInboxService>().Object,
                new Mock<ICompetitionRepository>().Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
        }

        [Test]
        public void NotFound_Returns404View()
        {
            var result = _controller.PageNotFound();

            var view = result as ViewResult;
            Assert.That(view, Is.Not.Null);
            Assert.That(view!.ViewName, Is.EqualTo("NotFound"));
            Assert.That(_controller.Response.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
            Assert.That(view.ViewData["StatusCode"], Is.EqualTo(404));
        }

        [TestCase(403)]
        [TestCase(429)]
        [TestCase(500)]
        public void NotFound_WhenReExecutedWithAnErrorCode_KeepsThatCode(int code)
        {
            var result = _controller.PageNotFound(code) as ViewResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(_controller.Response.StatusCode, Is.EqualTo(code));
            Assert.That(result!.ViewData["StatusCode"], Is.EqualTo(code));
        }

        [TestCase(200)]
        [TestCase(302)]
        [TestCase(0)]
        public void NotFound_WithANonErrorCode_FallsBackTo404(int code)
        {
            _controller.PageNotFound(code);

            Assert.That(_controller.Response.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
        }

        [Test]
        public void NotFound_IsAnonymousAndRoutedAsNotFound()
        {
            var method = typeof(HomeController).GetMethod(nameof(HomeController.PageNotFound))!;

            Assert.That(method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute), false), Is.Not.Empty);
            var actionName = (ActionNameAttribute?)method.GetCustomAttributes(typeof(ActionNameAttribute), false).SingleOrDefault();
            Assert.That(actionName?.Name, Is.EqualTo("NotFound"));
        }
    }
}
