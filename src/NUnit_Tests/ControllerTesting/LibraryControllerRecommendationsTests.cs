using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using SteamProject.Controllers;
using SteamProject.DAL.Abstract;
using SteamProject.Models;
using SteamProject.Services;

namespace NUnit_Tests.ControllerTesting
{
    [TestFixture]
    public class LibraryControllerRecommendationsTests
    {
        private Mock<IUserRepository> _userRepositoryMock;
        private LibraryController _controller;

        [SetUp]
        public void SetUp()
        {
            // A real (not mocked) UserManager so GetUserId resolves the
            // Identity user-id claim from the controller's ClaimsPrincipal.
            var userManager = new UserManager<IdentityUser>(
                new Mock<IUserStore<IdentityUser>>().Object,
                Options.Create(new IdentityOptions()),
                null, null, null, null, null, null, null);

            _userRepositoryMock = new Mock<IUserRepository>();
            _controller = new LibraryController(
                userManager,
                _userRepositoryMock.Object,
                new Mock<IGameRepository>().Object,
                new Mock<IUserGameInfoRepository>().Object,
                new Mock<ISteamService>().Object,
                new Mock<IIGDBGenresRepository>().Object);

            var httpContext = new DefaultHttpContext();
            httpContext.User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(
                    new[]
                    {
                        new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "test-user-id")
                    }));
            _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        }

        [Test]
        public void Recommendations_WhenUserNotFound_ReturnsUnauthorized()
        {
            // Arrange: the Identity principal exists, but there is no matching
            // SteamProject User row.
            _userRepositoryMock
                .Setup(m => m.GetUser("test-user-id"))
                .Returns((User)null!);

            // Act
            var result = _controller.Recommendations();

            // Assert — regression: before the fix, `user.Id` was dereferenced
            // on a null User and threw NullReferenceException (HTTP 500).
            Assert.That(result, Is.InstanceOf<UnauthorizedResult>());
        }
    }
}
