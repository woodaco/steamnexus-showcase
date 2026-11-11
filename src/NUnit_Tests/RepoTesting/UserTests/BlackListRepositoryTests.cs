using Microsoft.EntityFrameworkCore;
using Moq;
using SteamProject.DAL.Concrete;
using SteamProject.Models;

namespace NUnit_Tests.RepoTesting
{
    /// <summary>
    /// Deterministic coverage for the admin Unban path (RemoveFromBlackList), independent of E2E
    /// ordering/state: it must remove every row for the SteamId (idempotent over duplicates), leave
    /// other entries untouched, and report whether anything was removed.
    /// </summary>
    public class BlackListRepositoryTests
    {
        private Mock<SteamInfoDbContext> _mockContext = null!;
        private Mock<DbSet<BlackList>> _mockSet = null!;

        private void Arrange(params BlackList[] rows)
        {
            _mockSet = MockHelpers.GetMockDbSet(rows.AsQueryable());
            _mockContext = new Mock<SteamInfoDbContext>();
            _mockContext.Setup(c => c.Set<BlackList>()).Returns(_mockSet.Object);
            _mockContext.Setup(c => c.BlackLists).Returns(_mockSet.Object);
        }

        [Test]
        public void RemoveFromBlackList_RemovesEveryRowForThatSteamId_AndLeavesOthers()
        {
            Arrange(
                new BlackList { Id = 1, SteamId = "banned-1" },
                new BlackList { Id = 2, SteamId = "banned-1" }, // duplicate entry for the same id
                new BlackList { Id = 3, SteamId = "other" });
            var repo = new BlackListRepository(_mockContext.Object);

            var removed = repo.RemoveFromBlackList("banned-1");

            Assert.That(removed, Is.True);
            _mockSet.Verify(m => m.Remove(It.Is<BlackList>(b => b.SteamId == "banned-1")), Times.Exactly(2));
            _mockSet.Verify(m => m.Remove(It.Is<BlackList>(b => b.SteamId == "other")), Times.Never);
        }

        [Test]
        public void RemoveFromBlackList_UnknownSteamId_RemovesNothing_AndReturnsFalse()
        {
            Arrange(new BlackList { Id = 1, SteamId = "banned-1" });
            var repo = new BlackListRepository(_mockContext.Object);

            var removed = repo.RemoveFromBlackList("not-on-list");

            Assert.That(removed, Is.False);
            _mockSet.Verify(m => m.Remove(It.IsAny<BlackList>()), Times.Never);
        }
    }
}
