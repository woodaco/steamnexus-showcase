using Microsoft.EntityFrameworkCore;
using Moq;
using SteamProject.DAL.Concrete;
using SteamProject.Models;

namespace NUnit_Tests.RepoTesting
{
    /// <summary>
    /// Guards the read path of the messenger: opening a conversation must mark the whole
    /// backlog read in ONE database round-trip (a single SaveChanges), not one UPDATE per
    /// unread message. A 200-message backlog on a slow link is what this exists to keep cheap.
    /// </summary>
    public class InboxRepositoryTests
    {
        private const int RecipientId = 1;
        private const int SenderId = 2;

        private Mock<SteamInfoDbContext> _mockContext = null!;
        private Mock<DbSet<InboxMessage>> _mockSet = null!;

        private static InboxMessage Unread(int id)
        {
            return new InboxMessage
            {
                Id = id,
                RecipientId = RecipientId,
                SenderId = SenderId,
                IsRead = false,
                TimeStamp = new DateTime(2026, 10, 14, 15, 0, id, DateTimeKind.Utc),
            };
        }

        private void Arrange(params InboxMessage[] rows)
        {
            _mockSet = MockHelpers.GetMockDbSet(rows.AsQueryable());
            _mockContext = new Mock<SteamInfoDbContext>();
            _mockContext.Setup(c => c.Set<InboxMessage>()).Returns(_mockSet.Object);
            _mockContext.Setup(c => c.InboxMessages).Returns(_mockSet.Object);
        }

        [Test]
        public void MarkConversationRead_FiveUnreadMessages_SavesOnce_AndMarksAllRead()
        {
            InboxMessage m1 = Unread(1), m2 = Unread(2), m3 = Unread(3), m4 = Unread(4), m5 = Unread(5);
            var otherConversation = new InboxMessage { Id = 9, RecipientId = RecipientId, SenderId = 3, IsRead = false };
            var alreadyRead = new InboxMessage { Id = 10, RecipientId = RecipientId, SenderId = SenderId, IsRead = true };
            Arrange(m1, m2, m3, m4, m5, otherConversation, alreadyRead);

            var repo = new InboxRepository(_mockContext.Object);
            repo.MarkConversationRead(RecipientId, SenderId);

            // All five conversation rows read, the other two untouched
            Assert.Multiple(() =>
            {
                Assert.That(m1.IsRead, Is.True, "message 1 should be read");
                Assert.That(m2.IsRead, Is.True, "message 2 should be read");
                Assert.That(m3.IsRead, Is.True, "message 3 should be read");
                Assert.That(m4.IsRead, Is.True, "message 4 should be read");
                Assert.That(m5.IsRead, Is.True, "message 5 should be read");
                Assert.That(otherConversation.IsRead, Is.False, "other conversation must stay unread");
                Assert.That(alreadyRead.IsRead, Is.True, "already-read message stays read");
                // every unread row was staged as an update...
                _mockSet.Verify(m => m.Update(It.IsAny<InboxMessage>()), Times.Exactly(5));
                // ...and persisted in exactly one SaveChanges
                _mockContext.Verify(c => c.SaveChanges(), Times.Once());
            });
        }

        [Test]
        public void MarkConversationRead_NothingUnread_DoesNotSave()
        {
            Arrange(
                new InboxMessage { Id = 10, RecipientId = RecipientId, SenderId = SenderId, IsRead = true });

            var repo = new InboxRepository(_mockContext.Object);
            repo.MarkConversationRead(RecipientId, SenderId);

            _mockSet.Verify(m => m.Update(It.IsAny<InboxMessage>()), Times.Never);
            _mockContext.Verify(c => c.SaveChanges(), Times.Never);
        }
    }
}
