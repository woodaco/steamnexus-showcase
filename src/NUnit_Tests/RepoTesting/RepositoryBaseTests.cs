using Microsoft.EntityFrameworkCore;
using Moq;
using SteamProject.DAL.Concrete;
using SteamProject.Models;

namespace NUnit_Tests.RepoTesting
{
    /// <summary>
    /// Tests for Repository&lt;T&gt;.DeleteById — must not throw when the id is unknown,
    /// must call DbSet.Remove exactly once when the id exists, and must leave other rows untouched.
    /// </summary>
    public class RepositoryBaseTests
    {
        private Mock<SteamInfoDbContext> _mockContext = null!;
        private Mock<DbSet<BlackList>> _mockSet = null!;
        private BlackList? _capturedUpdate = null;

        private void Arrange(params BlackList[] rows)
        {
            var store = rows.ToList();
            _mockSet = MockHelpers.GetMockDbSet(store.AsQueryable());
            _mockSet
                .Setup(m => m.Find(It.IsAny<object?[]>()))
                .Returns((object?[]? keys) => keys != null && keys.Length > 0
                    ? store.FirstOrDefault(r => r.Id == (int)keys[0]!)
                    : null);
            _mockSet
                .Setup(m => m.FindAsync(It.IsAny<object?[]>()))
                .Returns((object?[]? keys) => new ValueTask<BlackList?>(keys != null && keys.Length > 0
                    ? store.FirstOrDefault(r => r.Id == (int)keys[0]!)
                    : null));
            _mockSet
                .Setup(m => m.Remove(It.IsAny<BlackList>()))
                .Callback<BlackList>(r => store.Remove(r));
            _mockContext = new Mock<SteamInfoDbContext>();
            _mockContext.Setup(c => c.Set<BlackList>()).Returns(_mockSet.Object);
            _mockContext.Setup(c => c.BlackLists).Returns(_mockSet.Object);
            _mockContext
                .Setup(c => c.Update(It.IsAny<BlackList>()))
                .Callback<BlackList>(r => _capturedUpdate = r);
            _mockContext
                .Setup(c => c.SaveChangesAsync())
                .Returns(() => Task.FromResult(1));
        }

        [Test]
        public void DeleteById_UnknownId_DoesNotThrow_AndRemovesNothing()
        {
            Arrange(new BlackList { Id = 1, SteamId = "abc" });
            var repo = new BlackListRepository(_mockContext.Object);

            Assert.DoesNotThrow(() => repo.DeleteById(999));
            _mockSet.Verify(m => m.Remove(It.IsAny<BlackList>()), Times.Never);
        }

        [Test]
        public void DeleteById_ExistingId_RemovesExactlyOnce()
        {
            Arrange(
                new BlackList { Id = 1, SteamId = "abc" },
                new BlackList { Id = 2, SteamId = "def" });
            var repo = new BlackListRepository(_mockContext.Object);

            repo.DeleteById(1);

            _mockSet.Verify(m => m.Remove(It.Is<BlackList>(b => b.Id == 1)), Times.Once);
            _mockSet.Verify(m => m.Remove(It.Is<BlackList>(b => b.Id == 2)), Times.Never);
        }

        [Test]
        public void DeleteById_AlreadyDeleted_IsIdempotent()
        {
            Arrange(new BlackList { Id = 1, SteamId = "abc" });
            var repo = new BlackListRepository(_mockContext.Object);

            repo.DeleteById(1);
            Assert.DoesNotThrow(() => repo.DeleteById(1));
            _mockSet.Verify(m => m.Remove(It.IsAny<BlackList>()), Times.Once);
        }

        // --- Async members ---

        [Test]
        public async Task FindByIdAsync_MissingId_ReturnsNull()
        {
            Arrange(new BlackList { Id = 1, SteamId = "abc" });
            var repo = new BlackListRepository(_mockContext.Object);

            var found = await repo.FindByIdAsync(999);

            Assert.That(found, Is.Null, "FindByIdAsync must return null for an unknown id");
        }

        [Test]
        public async Task FindByIdAsync_ExistingId_ReturnsEntity()
        {
            Arrange(new BlackList { Id = 1, SteamId = "abc" });
            var repo = new BlackListRepository(_mockContext.Object);

            var found = await repo.FindByIdAsync(1);

            Assert.That(found, Is.Not.Null);
            Assert.That(found!.Id, Is.EqualTo(1));
        }

        [Test]
        public async Task AddOrUpdateAsync_UpdatesAndSavesOnce()
        {
            Arrange(new BlackList { Id = 1, SteamId = "abc" });
            var repo = new BlackListRepository(_mockContext.Object);
            var entity = new BlackList { Id = 1, SteamId = "abc" };

            var result = await repo.AddOrUpdateAsync(entity);

            Assert.That(result, Is.SameAs(entity));
            Assert.That(_capturedUpdate, Is.SameAs(entity));
            _mockContext.Verify(c => c.SaveChangesAsync(), Times.Once);
        }

        [Test]
        public async Task AddOrUpdateAsync_Null_ThrowsArgumentNull()
        {
            Arrange(new BlackList { Id = 1, SteamId = "abc" });
            var repo = new BlackListRepository(_mockContext.Object);

            Assert.ThrowsAsync<ArgumentNullException>(() => repo.AddOrUpdateAsync(null!));
        }

        [Test]
        public async Task DeleteAsync_RemovesEntityAndSavesOnce()
        {
            Arrange(new BlackList { Id = 1, SteamId = "abc" });
            var repo = new BlackListRepository(_mockContext.Object);
            var entity = new BlackList { Id = 1, SteamId = "abc" };

            await repo.DeleteAsync(entity);

            _mockSet.Verify(m => m.Remove(entity), Times.Once);
            _mockContext.Verify(c => c.SaveChangesAsync(), Times.Once);
        }

        [Test]
        public async Task DeleteAsync_Null_ThrowsArgumentNull()
        {
            Arrange(new BlackList { Id = 1, SteamId = "abc" });
            var repo = new BlackListRepository(_mockContext.Object);

            Assert.ThrowsAsync<ArgumentNullException>(() => repo.DeleteAsync(null!));
        }

        [Test]
        public async Task DeleteByIdAsync_UnknownId_DoesNotThrow_AndRemovesNothing()
        {
            Arrange(new BlackList { Id = 1, SteamId = "abc" });
            var repo = new BlackListRepository(_mockContext.Object);

            await repo.DeleteByIdAsync(999);

            _mockSet.Verify(m => m.Remove(It.IsAny<BlackList>()), Times.Never);
            _mockContext.Verify(c => c.SaveChangesAsync(), Times.Never);
        }

        [Test]
        public async Task DeleteByIdAsync_ExistingId_RemovesExactlyOnce()
        {
            Arrange(
                new BlackList { Id = 1, SteamId = "abc" },
                new BlackList { Id = 2, SteamId = "def" });
            var repo = new BlackListRepository(_mockContext.Object);

            await repo.DeleteByIdAsync(1);

            _mockSet.Verify(m => m.Remove(It.Is<BlackList>(b => b.Id == 1)), Times.Once);
            _mockSet.Verify(m => m.Remove(It.Is<BlackList>(b => b.Id == 2)), Times.Never);
            _mockContext.Verify(c => c.SaveChangesAsync(), Times.Once);
        }

        [Test]
        public async Task DeleteByIdAsync_AlreadyDeleted_IsIdempotent()
        {
            Arrange(new BlackList { Id = 1, SteamId = "abc" });
            var repo = new BlackListRepository(_mockContext.Object);

            await repo.DeleteByIdAsync(1);
            await repo.DeleteByIdAsync(1);

            _mockSet.Verify(m => m.Remove(It.IsAny<BlackList>()), Times.Once);
            _mockContext.Verify(c => c.SaveChangesAsync(), Times.Once);
        }
    }
}
