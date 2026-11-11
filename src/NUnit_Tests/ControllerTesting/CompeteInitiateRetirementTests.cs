using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SteamProject.Controllers;
using SteamProject.DAL.Abstract;
using SteamProject.Models;
using SteamProject.Services;
using SteamProject.ViewModels;

namespace NUnit_Tests.ControllerTesting
{
    /// <summary>
    /// Regression guard for the retired Compete/Initiate POST: the old action bound the whole
    /// CompeteInitiateVM and handed competeIn.CurrentCompetition straight to
    /// ICompetitionRepository.AddOrUpdate, so a request-supplied Competition.Id silently
    /// overwrote an arbitrary existing row. Nothing may write a Competition whose Id came from
    /// the request. The Initiate page now hands off to the validated Compete/Create flow, which
    /// builds the Competition server-side with a fresh, DB-assigned Id.
    /// </summary>
    [TestFixture]
    public class CompeteInitiateRetirementTests
    {
        private const string TestAuthUserId = "auth-user-1";
        private const int TestUserId = 7;
        private const int TestGameRowId = 11;
        private const int TestGameAppId = 440;
        private const int RequestSuppliedCompetitionId = 4242;

        /// <summary>
        /// The mass-assigning POST action, if it still exists: a public instance Initiate taking
        /// exactly one CompeteInitiateVM. (The GET overload takes strings/ints, so it never matches.)
        /// </summary>
        private static MethodInfo? FindInitiatePostAction()
        {
            return typeof(CompeteController)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .SingleOrDefault(m =>
                    m.Name == "Initiate" &&
                    m.GetParameters().Length == 1 &&
                    m.GetParameters()[0].ParameterType == typeof(CompeteInitiateVM));
        }

        private static (CompeteController Controller, Mock<ICompetitionRepository> CompetitionRepo) BuildController()
        {
            var userManagerMock = new Mock<UserManager<IdentityUser>>(
                new Mock<IUserStore<IdentityUser>>().Object,
                Options.Create(new IdentityOptions()),
                null, null, null, null, null, null, null);
            userManagerMock
                .Setup(m => m.GetUserId(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
                .Returns(TestAuthUserId);

            var userRepositoryMock = new Mock<IUserRepository>();
            userRepositoryMock
                .Setup(m => m.GetUser(TestAuthUserId))
                .Returns(new User { Id = TestUserId, AspNetUserId = TestAuthUserId, SteamId = "STEAM_ME" });

            var gameRepositoryMock = new Mock<IGameRepository>();
            gameRepositoryMock
                .Setup(m => m.GetGameByAppId(TestGameAppId))
                .Returns(new Game { Id = TestGameRowId, AppId = TestGameAppId, Name = "Test Game" });
            gameRepositoryMock
                .Setup(m => m.GetGameById(TestGameRowId))
                .Returns(new Game { Id = TestGameRowId, AppId = TestGameAppId, Name = "Test Game" });

            var competitionRepoMock = new Mock<ICompetitionRepository>();

            // The real repository returns a materialized list, never null; keep the fake faithful
            // so Create's achievement path exercises the real code (empty game-achievement table
            // falls through to the schema backfill, which the SteamService mock answers with null,
            // handled by the controller's ?? guards).
            var gameAchievementRepoMock = new Mock<IGameAchievementRepository>();
            gameAchievementRepoMock
                .Setup(m => m.GetAchievementsFromGameId(TestGameRowId))
                .Returns(new List<GameAchievement>());

            var controller = new CompeteController(
                NullLogger<CompeteController>.Instance,
                new Mock<ISteamService>().Object,
                userRepositoryMock.Object,
                new Mock<IFriendRepository>().Object,
                gameAchievementRepoMock.Object,
                new Mock<IUserAchievementRepository>().Object,
                gameRepositoryMock.Object,
                new Mock<IUserGameInfoRepository>().Object,
                competitionRepoMock.Object,
                new Mock<ICompetitionPlayerRepository>().Object,
                new Mock<ICompetitionGameAchievementRepository>().Object,
                userManagerMock.Object,
                new Mock<IInboxService>().Object,
                new Mock<IStatusRepository>().Object,
                new Mock<ICompetitionVoteRepository>().Object);

            var httpContext = new DefaultHttpContext();
            httpContext.User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(
                    new[]
                    {
                        new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, TestAuthUserId)
                    }));
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            return (controller, competitionRepoMock);
        }

        [Test]
        public void InitiatePost_NeverWritesARequestSuppliedCompetitionId()
        {
            var postInitiate = FindInitiatePostAction();
            if (postInitiate is null)
            {
                // The mass-assigning endpoint is retired: no Initiate POST path can carry a
                // request-supplied Competition.Id into ICompetitionRepository.AddOrUpdate anymore.
                // Retirement is the fix; assert it explicitly rather than passing vacuously.
                Assert.That(postInitiate, Is.Null, "retired Initiate POST must stay retired");
                return;
            }

            // If the endpoint still exists it must never hand a request-supplied Competition.Id
            // to AddOrUpdate. Build a VM whose CurrentCompetition.Id is arbitrary and check every
            // Competition the action writes.
            var vm = new CompeteInitiateVM
            {
                MySteamId = "STEAM_ME",
                MyFriendId = "STEAM_FRIEND",
            };
            vm.ChosenGame.Id = TestGameRowId;
            vm.ChosenGame.AppId = TestGameAppId;
            vm.CurrentCompetition.Id = RequestSuppliedCompetitionId;

            var (controller, competitionRepoMock) = BuildController();
            postInitiate.Invoke(controller, new object[] { vm });

            competitionRepoMock.Verify(
                r => r.AddOrUpdate(It.Is<Competition>(c => c.Id == RequestSuppliedCompetitionId)),
                Times.Never,
                "The Initiate POST must not write a Competition whose Id came from the request; " +
                "AddOrUpdate (context Update + SaveChanges) then silently overwrites an arbitrary existing row.");
        }

        [Test]
        public void CompeteCreateVM_CarriesNoCompetitionId_HandoffTargetCannotMassAssignOne()
        {
            // The Initiate form now posts to Create, whose binding model must never expose a
            // Competition Id: giving CompeteCreateVM an Id property would re-open the exact bug
            // this retirement closed (a request-supplied Competition.Id reaching AddOrUpdate).
            var propertyNames = typeof(CompeteCreateVM)
                .GetProperties()
                .Select(p => p.Name)
                .ToList();

            Assert.That(propertyNames, Does.Not.Contain("Id"),
                "CompeteCreateVM must not gain a Competition Id property; that is how the retired " +
                "Initiate POST overwrote arbitrary competition rows.");
        }

        [Test]
        public async Task CreatePost_BuildsTheCompetitionServerSide_NotFromTheRequest()
        {
            // The handoff target itself: the Competition is constructed by the action with the
            // creator from the session user and the game resolved from the posted AppId; its Id is
            // left default so the database assigns it. Nothing here can receive a Competition.Id.
            var (controller, competitionRepoMock) = BuildController();
            var vm = new CompeteCreateVM
            {
                SteamId = "STEAM_ME",
                OpponentId = "STEAM_FRIEND",
                GameAppId = TestGameAppId,
                AchievementDisplayNames = new List<string>(),
                CompStartTime = new DateTime(2026, 11, 5, 12, 0, 0),
                CompEndTime = new DateTime(2026, 11, 12, 12, 0, 0),
            };

            var result = await controller.Create(vm);

            Assert.That(result, Is.InstanceOf<RedirectToActionResult>());
            competitionRepoMock.Verify(
                r => r.AddOrUpdate(It.Is<Competition>(c =>
                    c.Id == 0 &&
                    c.CreatorId == TestUserId &&
                    c.GameId == TestGameRowId)),
                Times.Once,
                "Create must build the Competition with a fresh DB-assigned Id (0 before SaveChanges), " +
                "a session-derived CreatorId and a server-resolved GameId.");
        }
    }
}
