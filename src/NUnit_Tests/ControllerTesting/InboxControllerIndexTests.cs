using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using SteamProject.Controllers;
using SteamProject.DAL.Abstract;
using SteamProject.Models;

namespace NUnit_Tests.ControllerTesting
{
    [TestFixture]
    public class InboxControllerIndexTests
    {
        private Mock<UserManager<IdentityUser>> _userManagerMock = null!;
        private Mock<IUserRepository> _userRepositoryMock = null!;
        private Mock<IInboxRepository> _inboxRepositoryMock = null!;
        private InboxController _controller = null!;

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
            _inboxRepositoryMock = new Mock<IInboxRepository>();

            _controller = new InboxController(
                _userManagerMock.Object,
                _userRepositoryMock.Object,
                _inboxRepositoryMock.Object);

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
        public void Index_WhenUserRowMissing_ReturnsEmptyViewAndSkipsInboxLookup()
        {
            // Arrange: the Identity principal exists, but there is no matching
            // SteamProject User row (Steam-first login whose provisioning
            // failed, so EnsureDomainUserMiddleware never wrote a row).
            _userRepositoryMock
                .Setup(m => m.GetUser("test-user-id"))
                .Returns((User)null!);

            // Act
            var result = _controller.Index();

            // Assert — regression: before the fix, `user.Id` was dereferenced
            // on a null User and threw NullReferenceException (HTTP 500) on
            // /Inbox. Now the view renders with an empty model and the inbox
            // repository is never touched.
            Assert.That(result, Is.InstanceOf<ViewResult>());
            var view = (ViewResult)result;
            Assert.That(view.Model, Is.InstanceOf<List<InboxMessage>>());
            Assert.That((List<InboxMessage>)view.Model!, Is.Empty);
            _inboxRepositoryMock.Verify(m => m.GetInboxMessages(It.IsAny<int>()), Times.Never);
        }

        [Test]
        public void Index_WhenUserExists_ReturnsMessageList()
        {
            // Arrange: a real User row and a non-empty inbox.
            var user = new User
            {
                Id = 1,
                AspNetUserId = "test-user-id"
            };
            _userRepositoryMock
                .Setup(m => m.GetUser("test-user-id"))
                .Returns(user);
            var messages = new List<InboxMessage>
            {
                new InboxMessage { Id = 2, RecipientId = 1, IsRead = true },
                new InboxMessage { Id = 1, RecipientId = 1, IsRead = false }
            };
            _inboxRepositoryMock
                .Setup(m => m.GetInboxMessages(1))
                .Returns(messages);

            // Act
            var result = _controller.Index();

            // Assert — the messages come back, newest-first by Id.
            Assert.That(result, Is.InstanceOf<ViewResult>());
            var view = (ViewResult)result;
            var returned = (List<InboxMessage>)view.Model!;
            Assert.That(returned, Has.Count.EqualTo(2));
            Assert.That(returned[0].Id, Is.EqualTo(2));
            Assert.That(returned[1].Id, Is.EqualTo(1));
            _inboxRepositoryMock.Verify(m => m.GetInboxMessages(1), Times.Once);
        }
    }
}
