using Microsoft.EntityFrameworkCore;
using Moq;
using SteamProject.DAL.Concrete;
using SteamProject.Models;

namespace NUnit_Tests.RepoTesting
{
    /// <summary>
    /// Pins the RootId-scoping ownership fix in FriendRepository (commit ab7f9b28).
    /// A single SteamId can appear as a friend of several users, so a SteamId-only lookup can
    /// resolve to another user's row, and the nickname callers in SteamController treat a
    /// non-null result as proof of ownership before writing. These tests fail if the
    /// "f.RootId == rootId" predicate (or GetFriends' RootId filter) is dropped.
    /// </summary>
    public class FriendRepositoryTests
    {
        private Mock<SteamInfoDbContext> _mockContext = null!;
        private Mock<DbSet<Friend>> _mockSet = null!;

        private void Arrange(params Friend[] rows)
        {
            _mockSet = MockHelpers.GetMockDbSet(rows.AsQueryable());
            _mockContext = new Mock<SteamInfoDbContext>();
            _mockContext.Setup(c => c.Set<Friend>()).Returns(_mockSet.Object);
        }

        [Test]
        public void GetSpecificFriend_SameSteamIdOwnedByAnotherRoot_ReturnsNull()
        {
            // The only row with this SteamId belongs to a DIFFERENT user (RootId=2).
            Arrange(new Friend { Id = 101, RootId = 2, SteamId = "76561198000000001" });
            var repo = new FriendRepository(_mockContext.Object);

            var result = repo.GetSpecificFriend("76561198000000001", rootId: 1);

            Assert.That(result, Is.Null);
        }

        [Test]
        public void GetSpecificFriend_ReturnsTheOwningRow_WhenRootMatches()
        {
            // Same SteamId under two users. The foreign row (RootId=2) is listed FIRST so a
            // SteamId-only lookup would wrongly pick it; only the RootId predicate returns the
            // caller's own row (RootId=1, Id=202).
            const string shared = "76561198000000002";
            Arrange(
                new Friend { Id = 201, RootId = 2, SteamId = shared, Nickname = "not-mine" },
                new Friend { Id = 202, RootId = 1, SteamId = shared, Nickname = "mine" });
            var repo = new FriendRepository(_mockContext.Object);

            var result = repo.GetSpecificFriend(shared, rootId: 1);

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Id, Is.EqualTo(202));
            Assert.That(result.RootId, Is.EqualTo(1));
        }

        [Test]
        public void GetFriends_ReturnsOnlyRowsBelongingToThatRoot()
        {
            Arrange(
                new Friend { Id = 1, RootId = 1, SteamId = "76561198000000003" },
                new Friend { Id = 2, RootId = 2, SteamId = "76561198000000004" }, // another user's row
                new Friend { Id = 3, RootId = 1, SteamId = "76561198000000005" });
            var repo = new FriendRepository(_mockContext.Object);

            var friends = repo.GetFriends(1);

            Assert.That(friends, Has.Count.EqualTo(2));
            Assert.That(friends.Any(f => f.RootId != 1), Is.False);
            Assert.That(friends.Select(f => f.Id), Is.EquivalentTo(new[] { 1, 3 }));
        }
    }
}
