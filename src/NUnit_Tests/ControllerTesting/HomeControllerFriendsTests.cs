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
using SteamProject.ViewModels;

namespace NUnit_Tests.ControllerTesting
{
    [TestFixture]
    public class HomeControllerFriendsTests
    {
        private Mock<UserManager<IdentityUser>> _userManagerMock = null!;
        private Mock<IUserRepository> _userRepositoryMock = null!;
        private Mock<IFriendRepository> _friendRepositoryMock = null!;
        private HomeController _controller = null!;

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
            _friendRepositoryMock = new Mock<IFriendRepository>();

            _controller = new HomeController(
                _userManagerMock.Object,
                _userRepositoryMock.Object,
                new Mock<IGameRepository>().Object,
                new Mock<IUserGameInfoRepository>().Object,
                new Mock<ISteamService>().Object,
                _friendRepositoryMock.Object,
                new Mock<IInboxService>().Object,
                new Mock<ICompetitionRepository>().Object);

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
        public async Task Friends_WhenUserRowMissing_ReturnsViewResult()
        {
            // Arrange: the Identity principal exists, but there is no matching
            // SteamProject User row.
            _userRepositoryMock
                .Setup(m => m.GetUser("test-user-id"))
                .Returns((User)null!);

            // Act
            var result = await _controller.Friends();

            // Assert — regression: before the fix, `user.Friends` was dereferenced
            // on a null User and threw NullReferenceException (HTTP 500) while
            // /Home/Dashboard, which guards the same case, rendered fine.
            Assert.That(result, Is.InstanceOf<ViewResult>());
        }

        [Test]
        public async Task Friends_MarksOnlyFriendsWhoseSteamIdBelongsToASiteUserAsLinked()
        {
            // Arrange: a real User row with no pre-loaded friends, two friend rows —
            // one whose SteamId belongs to a site user, one whose does not.
            var user = new User
            {
                Id = 1,
                AspNetUserId = "test-user-id",
                SteamId = "76561198000000001"
            };
            _userRepositoryMock
                .Setup(m => m.GetUser("test-user-id"))
                .Returns(user);
            _userRepositoryMock
                .Setup(m => m.GetAllUsers())
                .Returns(new[]
                {
                    new User { Id = 1, AspNetUserId = "test-user-id", SteamId = "76561198000000001" },
                    new User { Id = 2, AspNetUserId = "other-id", SteamId = "76561198000000002" }
                });

            var linkedFriend = new Friend
            {
                Id = 10,
                RootId = 1,
                SteamId = "76561198000000002",
                SteamName = "SiteUser"
            };
            var unlinkedFriend = new Friend
            {
                Id = 11,
                RootId = 1,
                SteamId = "76561198000000003",
                SteamName = "SteamOnly"
            };
            _friendRepositoryMock
                .Setup(m => m.GetFriends(1))
                .Returns(new List<Friend> { linkedFriend, unlinkedFriend });

            // Act
            var result = await _controller.Friends();

            // Assert — Linked is set on the site-user friend only, and the page
            // model carries both friends.
            Assert.That(result, Is.InstanceOf<ViewResult>());
            var vm = (FriendsPageVM)((ViewResult)result).Model!;
            Assert.That(vm.Friends, Has.Count.EqualTo(2));
            Assert.That(linkedFriend.Linked, Is.True);
            Assert.That(unlinkedFriend.Linked, Is.Null);
        }
    }
}
