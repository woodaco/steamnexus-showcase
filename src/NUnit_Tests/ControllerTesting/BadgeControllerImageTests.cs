using Microsoft.AspNetCore.Http;
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
    public class BadgeControllerImageTests
    {
        private Mock<IBadgeRepository> _badgeRepoMock = null!;
        private DefaultHttpContext _httpContext = null!;
        private IHeaderDictionary _headers = null!;
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

            _httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext { HttpContext = _httpContext };
            _headers = _httpContext.Response.Headers;
        }

        [Test]
        public void GetBadgeImage_SetsETagHeader()
        {
            var imageBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A };
            var badge = new Badge { Id = 1, Name = "Test", Image = imageBytes };
            _badgeRepoMock.Setup(m => m.FindById(1)).Returns(badge);

            var result = _controller.GetBadgeImage(1);

            Assert.That(result, Is.InstanceOf<FileContentResult>());
            Assert.That(_headers["ETag"], Is.Not.Null);
            var etag = _headers["ETag"].ToString();
            Assert.That(etag, Does.StartWith("\"").And.EndWith("\""));
        }

        [Test]
        public void GetBadgeImage_SetsCacheControlHeader()
        {
            var imageBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A };
            var badge = new Badge { Id = 1, Name = "Test", Image = imageBytes };
            _badgeRepoMock.Setup(m => m.FindById(1)).Returns(badge);

            var result = _controller.GetBadgeImage(1);

            Assert.That(result, Is.InstanceOf<FileContentResult>());
            var cacheControl = _headers["Cache-Control"].ToString();
            Assert.That(cacheControl, Does.Contain("max-age"));
        }

        [Test]
        public void GetBadgeImage_Returns304_WhenIfNoneMatchMatches()
        {
            var imageBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A };
            var badge = new Badge { Id = 1, Name = "Test", Image = imageBytes };
            _badgeRepoMock.Setup(m => m.FindById(1)).Returns(badge);

            // First call to get the ETag
            _controller.GetBadgeImage(1);
            string etag = _headers["ETag"].ToString();
            Assert.That(etag, Is.Not.Null);

            // Set If-None-Match and call again
            _httpContext.Request.Headers["If-None-Match"] = etag;
            var result = _controller.GetBadgeImage(1);

            Assert.That(result, Is.InstanceOf<StatusCodeResult>());
            var statusCodeResult = (StatusCodeResult)result;
            Assert.That(statusCodeResult.StatusCode, Is.EqualTo(304));
        }
    }
}
