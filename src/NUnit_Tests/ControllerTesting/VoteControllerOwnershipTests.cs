using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using SteamProject.Controllers;
using SteamProject.DAL.Abstract;
using SteamProject.Models;
using SteamProject.Models.DTO;

namespace NUnit_Tests.ControllerTesting
{
    [TestFixture]
    public class VoteControllerOwnershipTests
    {
        private Mock<ICompetitionVoteRepository> _voteRepoMock = null!;
        private Mock<ICompetitionPlayerRepository> _playerRepoMock = null!;
        private Mock<UserManager<IdentityUser>> _userManagerMock = null!;
        private Mock<IUserRepository> _userRepositoryMock = null!;
        private VoteController _controller = null!;

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
            _voteRepoMock = new Mock<ICompetitionVoteRepository>();
            _playerRepoMock = new Mock<ICompetitionPlayerRepository>();

            _controller = new VoteController(
                _voteRepoMock.Object,
                _playerRepoMock.Object,
                _userManagerMock.Object,
                _userRepositoryMock.Object);

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
        public async Task UpdateCompetitionVote_WhenVoteNotFound_ReturnsNotFound()
        {
            // Arrange
            _userRepositoryMock
                .Setup(m => m.GetUser("test-user-id"))
                .Returns(new User { Id = 1, AspNetUserId = "test-user-id", SteamId = "STEAM_0:1:42" });
            _voteRepoMock
                .Setup(m => m.FindById(99))
                .Returns((CompetitionVote)null!);

            var req = new CompetitionVoteRequest { Id = 99, CompetitionId = 1, WantsToPlayAgain = true };

            // Act
            var result = await _controller.UpdateCompetitionVote(99, req);

            // Assert — repository's UpdateVoteAsync must never be called when the vote is absent.
            Assert.That(result, Is.InstanceOf<NotFoundResult>());
            _voteRepoMock.Verify(m => m.UpdateVoteAsync(It.IsAny<CompetitionVote>()), Times.Never);
        }

        [Test]
        public async Task UpdateCompetitionVote_WhenDifferentUser_Returns403()
        {
            // Arrange: a vote owned by userId=5, caller is userId=1.
            var vote = new CompetitionVote { Id = 1, UserId = 5, WantsToPlayAgain = false };
            _userRepositoryMock
                .Setup(m => m.GetUser("test-user-id"))
                .Returns(new User { Id = 1, AspNetUserId = "test-user-id", SteamId = "STEAM_0:1:42" });
            _voteRepoMock
                .Setup(m => m.FindById(1))
                .Returns(vote);
            _playerRepoMock
                .Setup(m => m.GetAllForCompetition(1))
                .Returns(new List<CompetitionPlayer>
                {
                    new CompetitionPlayer { SteamId = "STEAM_0:1:42" }
                });

            var req = new CompetitionVoteRequest { Id = 1, CompetitionId = 1, WantsToPlayAgain = true };

            // Act
            var result = await _controller.UpdateCompetitionVote(1, req);

            // Assert — ownership mismatch returns 403 and UpdateVoteAsync is never invoked.
            Assert.That(result, Is.InstanceOf<StatusCodeResult>());
            var statusCodeResult = (StatusCodeResult)result;
            Assert.That(statusCodeResult.StatusCode, Is.EqualTo(403));
            _voteRepoMock.Verify(m => m.UpdateVoteAsync(It.IsAny<CompetitionVote>()), Times.Never);
        }

        [Test]
        public async Task UpdateCompetitionVote_OwnerStillReturnsOk()
        {
            // Arrange: vote owned by the caller, who is a participant.
            var vote = new CompetitionVote { Id = 1, UserId = 1, WantsToPlayAgain = false };
            _userRepositoryMock
                .Setup(m => m.GetUser("test-user-id"))
                .Returns(new User { Id = 1, AspNetUserId = "test-user-id", SteamId = "STEAM_0:1:42" });
            _voteRepoMock
                .Setup(m => m.FindById(1))
                .Returns(vote);
            _playerRepoMock
                .Setup(m => m.GetAllForCompetition(1))
                .Returns(new List<CompetitionPlayer>
                {
                    new CompetitionPlayer { SteamId = "STEAM_0:1:42" }
                });

            var req = new CompetitionVoteRequest { Id = 1, CompetitionId = 1, WantsToPlayAgain = true };

            // Act
            var result = await _controller.UpdateCompetitionVote(1, req);

            // Assert — owner's PUT returns Ok and the vote is persisted.
            Assert.That(result, Is.InstanceOf<OkResult>());
            _voteRepoMock.Verify(m => m.UpdateVoteAsync(It.Is<CompetitionVote>(v => v.WantsToPlayAgain == true)), Times.Once);
        }

        [Test]
        public async Task UpdateCompetitionVote_NonParticipant_Returns403()
        {
            // Arrange: caller owns the vote but is not a participant of the competition.
            var vote = new CompetitionVote { Id = 1, UserId = 1, WantsToPlayAgain = false };
            _userRepositoryMock
                .Setup(m => m.GetUser("test-user-id"))
                .Returns(new User { Id = 1, AspNetUserId = "test-user-id", SteamId = "STEAM_0:1:99" });
            _voteRepoMock
                .Setup(m => m.FindById(1))
                .Returns(vote);
            _playerRepoMock
                .Setup(m => m.GetAllForCompetition(1))
                .Returns(new List<CompetitionPlayer>
                {
                    new CompetitionPlayer { SteamId = "STEAM_0:1:42" }
                });

            var req = new CompetitionVoteRequest { Id = 1, CompetitionId = 1, WantsToPlayAgain = true };

            // Act
            var result = await _controller.UpdateCompetitionVote(1, req);

            // Assert — non-participant returns 403.
            Assert.That(result, Is.InstanceOf<StatusCodeResult>());
            var statusCodeResult = (StatusCodeResult)result;
            Assert.That(statusCodeResult.StatusCode, Is.EqualTo(403));
            _voteRepoMock.Verify(m => m.UpdateVoteAsync(It.IsAny<CompetitionVote>()), Times.Never);
        }
    }
}
