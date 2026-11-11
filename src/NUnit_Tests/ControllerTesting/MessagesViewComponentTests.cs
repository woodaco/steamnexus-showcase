using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using SteamProject.DAL.Abstract;
using SteamProject.Models;
using SteamProject.ViewComponents;

namespace NUnit_Tests.ControllerTesting
{
    [TestFixture]
    public class MessagesViewComponentTests
    {
        private Mock<UserManager<IdentityUser>> _userManagerMock = null!;
        private Mock<IUserRepository> _userRepositoryMock = null!;
        private MessagesViewComponent _component = null!;

        [SetUp]
        public void SetUp()
        {
            _userManagerMock = new Mock<UserManager<IdentityUser>>(
                new Mock<IUserStore<IdentityUser>>().Object,
                Options.Create(new IdentityOptions()),
                null, null, null, null, null, null, null);
            _userManagerMock
                .Setup(m => m.GetUserId(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
                .Returns("test-user-id");

            _userRepositoryMock = new Mock<IUserRepository>();

            _component = new MessagesViewComponent(
                _userRepositoryMock.Object,
                _userManagerMock.Object);
        }

        [Test]
        public void Invoke_WhenUserRowMissing_ReturnsNullSentinel()
        {
            // Arrange: the Identity principal exists, but there is no matching
            // SteamProject User row.
            _userRepositoryMock
                .Setup(m => m.GetUser("test-user-id"))
                .Returns((User)null!);

            // Set up the ViewComponent's HttpContext so it has an authenticated user.
            var httpContext = new DefaultHttpContext();
            httpContext.User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(
                    new[]
                    {
                        new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "test-user-id")
                    }));
            var viewContext = new ViewContext();
            viewContext.HttpContext = httpContext;
            typeof(ViewComponent).GetProperty("ViewContext", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.GetField)?.SetValue(_component, viewContext);

            // Act
            var result = _component.Invoke();

            // Assert — regression: before the fix, `GetUser(id).Id` threw
            // NullReferenceException (HTTP 500) on every page render.
            Assert.That(result, Is.InstanceOf<ViewViewComponentResult>());
            var viewResult = (ViewViewComponentResult)result;
            Assert.That(viewResult.ViewData.Model, Is.EqualTo("null"));
        }

        [Test]
        public void Invoke_WhenUserRowExists_ReturnsUserId()
        {
            // Arrange
            var user = new User { Id = 42, AspNetUserId = "test-user-id" };
            _userRepositoryMock
                .Setup(m => m.GetUser("test-user-id"))
                .Returns(user);

            var httpContext = new DefaultHttpContext();
            httpContext.User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(
                    new[]
                    {
                        new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "test-user-id")
                    }));
            var viewContext = new ViewContext();
            viewContext.HttpContext = httpContext;
            typeof(ViewComponent).GetProperty("ViewContext", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.GetField)?.SetValue(_component, viewContext);

            // Act
            var result = _component.Invoke();

            // Assert
            Assert.That(result, Is.InstanceOf<ViewViewComponentResult>());
            var viewResult = (ViewViewComponentResult)result;
            Assert.That(viewResult.ViewData.Model, Is.EqualTo("42"));
        }

        [Test]
        public void Invoke_WhenAnonymous_ReturnsNullSentinel()
        {
            // Arrange: no authenticated user.
            _userManagerMock
                .Setup(m => m.GetUserId(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
                .Returns((string?)null);

            // Act
            var result = _component.Invoke();

            // Assert
            Assert.That(result, Is.InstanceOf<ViewViewComponentResult>());
            var viewResult = (ViewViewComponentResult)result;
            Assert.That(viewResult.ViewData.Model, Is.EqualTo("null"));
        }
    }
}
