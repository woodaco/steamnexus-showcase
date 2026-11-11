using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using SteamProject.DAL.Abstract;
using SteamProject.Middlewares;
using SteamProject.Models;

namespace NUnit_Tests.MiddlewareTesting
{
    [TestFixture]
    public class MiddlewareTests
    {
        private Mock<UserManager<IdentityUser>> _userManagerMock = null!;
        private Mock<IUserRepository> _userRepositoryMock = null!;
        private Mock<IServiceScopeFactory> _scopeFactoryMock = null!;

        [SetUp]
        public void SetUp()
        {
            // Mocked UserManager so the identity user-id is a controlled value.
            _userManagerMock = new Mock<UserManager<IdentityUser>>(
                new Mock<IUserStore<IdentityUser>>().Object,
                Options.Create(new IdentityOptions()),
                null, null, null, null, null, null, null);
            _userManagerMock
                .Setup(m => m.GetUserId(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
                .Returns("test-user-id");

            _userRepositoryMock = new Mock<IUserRepository>();

            // A scope whose ServiceProvider resolves the two dependencies the
            // middlewares pull out of the DI container.
            var services = new ServiceCollection()
                .AddSingleton(_userRepositoryMock.Object)
                .AddSingleton(_userManagerMock.Object)
                .BuildServiceProvider();
            var scope = new Mock<IServiceScope>();
            scope.Setup(s => s.ServiceProvider).Returns(services);
            _scopeFactoryMock = new Mock<IServiceScopeFactory>();
            _scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scope.Object);
        }

        private static DefaultHttpContext NewContext() => new DefaultHttpContext();

        [Test]
        public async Task Messages_PopulatesMessengerId_WhenUserRowExists()
        {
            // Arrange: an authenticated principal with a matching User row.
            var user = new User { Id = 42, AspNetUserId = "test-user-id" };
            _userRepositoryMock
                .Setup(m => m.GetUser("test-user-id"))
                .Returns(user);

            var context = NewContext();

            // Act
            var middleware = new MessagesMiddleware(_ => Task.CompletedTask);
            await middleware.InvokeAsync(context, _scopeFactoryMock.Object);

            // Assert — regression: before the fix, a missing User row left
            // context.Items["MessengerId"] unset (a silent 500 downstream).
            Assert.That(context.Items["MessengerId"], Is.EqualTo("42"));
        }

        [Test]
        public async Task Messages_PopulatesMessengerIdAsNull_WhenUserRowMissing()
        {
            // Arrange: authenticated principal, but no matching User row.
            _userRepositoryMock
                .Setup(m => m.GetUser("test-user-id"))
                .Returns((User)null!);

            var context = NewContext();

            // Act
            var middleware = new MessagesMiddleware(_ => Task.CompletedTask);
            await middleware.InvokeAsync(context, _scopeFactoryMock.Object);

            // Assert — the item MUST exist even on a missing row.
            Assert.That(context.Items["MessengerId"], Is.EqualTo("null"));
        }

        [Test]
        public async Task Messages_PopulatesMessengerIdAsNull_WhenUnauthenticated()
        {
            // Arrange: no identity principal at all.
            _userManagerMock
                .Setup(m => m.GetUserId(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
                .Returns((string?)null);

            var context = NewContext();

            // Act
            var middleware = new MessagesMiddleware(_ => Task.CompletedTask);
            await middleware.InvokeAsync(context, _scopeFactoryMock.Object);

            // Assert — key present with the sentinel, and the repository is
            // never consulted for an anonymous request.
            Assert.That(context.Items["MessengerId"], Is.EqualTo("null"));
            _userRepositoryMock.Verify(m => m.GetUser(It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task Theme_PopulatesTheme_WhenUserHasOne()
        {
            // Arrange: authenticated principal with a User row that carries a theme.
            var user = new User { Id = 7, AspNetUserId = "test-user-id", Theme = "dark" };
            _userRepositoryMock
                .Setup(m => m.GetUser("test-user-id"))
                .Returns(user);

            var context = NewContext();

            // Act
            var middleware = new ThemeMiddleware(_ => Task.CompletedTask);
            await middleware.InvokeAsync(context, _scopeFactoryMock.Object);

            // Assert — regression: before the fix, a missing User row left
            // context.Items["Theme"] unset.
            Assert.That(context.Items["Theme"], Is.EqualTo("dark"));
        }

        [Test]
        public async Task Theme_PopulatesLight_WhenUserRowMissing()
        {
            // Arrange: authenticated principal, but no matching User row.
            _userRepositoryMock
                .Setup(m => m.GetUser("test-user-id"))
                .Returns((User)null!);

            var context = NewContext();

            // Act
            var middleware = new ThemeMiddleware(_ => Task.CompletedTask);
            await middleware.InvokeAsync(context, _scopeFactoryMock.Object);

            // Assert — the item MUST exist and fall back to light.
            Assert.That(context.Items["Theme"], Is.EqualTo("light"));
        }

        [Test]
        public async Task Theme_PopulatesLight_WhenUserThemeIsNull()
        {
            // Arrange: authenticated principal, User row exists but Theme is null.
            var user = new User { Id = 7, AspNetUserId = "test-user-id", Theme = null };
            _userRepositoryMock
                .Setup(m => m.GetUser("test-user-id"))
                .Returns(user);

            var context = NewContext();

            // Act
            var middleware = new ThemeMiddleware(_ => Task.CompletedTask);
            await middleware.InvokeAsync(context, _scopeFactoryMock.Object);

            // Assert — a null Theme must still yield a populated, default value.
            Assert.That(context.Items["Theme"], Is.EqualTo("light"));
        }

        [Test]
        public async Task Theme_PopulatesLight_WhenUnauthenticated()
        {
            // Arrange: no identity principal at all.
            _userManagerMock
                .Setup(m => m.GetUserId(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
                .Returns((string?)null);

            var context = NewContext();

            // Act
            var middleware = new ThemeMiddleware(_ => Task.CompletedTask);
            await middleware.InvokeAsync(context, _scopeFactoryMock.Object);

            // Assert — key present with the default; repository never consulted.
            Assert.That(context.Items["Theme"], Is.EqualTo("light"));
            _userRepositoryMock.Verify(m => m.GetUser(It.IsAny<string>()), Times.Never);
        }
    }
}
