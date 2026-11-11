using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using SteamProject.Controllers;
using SteamProject.DAL.Abstract;
using SteamProject.Models;
using SteamProject.Models.Awards.Abstract;
using SteamProject.Services;

namespace NUnit_Tests.ControllerTesting
{
    [TestFixture]
    public class BadgeControllerNullImageTests
    {
        private Mock<IBadgeRepository> _badgeRepoMock = null!;
        private BadgeController _controller = null!;

        [SetUp]
        public void SetUp()
        {
            var userManagerMock = new Mock<UserManager<IdentityUser>>(
                new Mock<IUserStore<IdentityUser>>().Object,
                Options.Create(new IdentityOptions()),
                null, null, null, null, null, null, null);

            _badgeRepoMock = new Mock<IBadgeRepository>();
            var userBadgeRepoMock = new Mock<IUserBadgeRepository>();
            var awardConditions = new IAwardCondition[0];
            var userRepoMock = new Mock<IUserRepository>();
            var inboxServiceMock = new Mock<IInboxService>();

            _controller = new BadgeController(
                userManagerMock.Object,
                _badgeRepoMock.Object,
                userBadgeRepoMock.Object,
                awardConditions,
                userRepoMock.Object,
                inboxServiceMock.Object);
        }

        [Test]
        public void GetBadgeImage_WhenBadgeImageIsNull_ReturnsNotFound()
        {
            var badge = new Badge { Id = 1, Name = "Test Badge", Image = null };
            _badgeRepoMock.Setup(m => m.FindById(1)).Returns(badge);

            var result = _controller.GetBadgeImage(1);

            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
        }

        [Test]
        public void GetBadgeImage_WhenBadgeImageIsEmpty_ReturnsNotFound()
        {
            var badge = new Badge { Id = 2, Name = "Test Badge", Image = new byte[0] };
            _badgeRepoMock.Setup(m => m.FindById(2)).Returns(badge);

            var result = _controller.GetBadgeImage(2);

            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
        }

        [Test]
        public void GetBadgeDetails_WhenBadgeImageIsNull_ReturnsNotFound()
        {
            var badge = new Badge { Id = 1, Name = "Test Badge", Image = null };
            _badgeRepoMock.Setup(m => m.FindById(1)).Returns(badge);

            var result = _controller.GetBadgeDetails(1);

            Assert.That(result.Result, Is.InstanceOf<NotFoundObjectResult>());
        }
    }
}
