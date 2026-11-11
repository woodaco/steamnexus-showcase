using Moq;
using SteamProject.DAL.Abstract;
using SteamProject.Models;
using SteamProject.Services;

namespace NUnit_Tests.ServiceTesting
{
    /// <summary>
    /// Proves the messenger write side stamps messages with a UTC instant, not the
    /// web server's local clock. <c>DateTime.Now</c> is Kind=Local on the dev box but
    /// Kind=Utc on the Azure deployment, so the same column mixed two clocks and
    /// ordering (OrderBy(TimeStamp) in InboxRepository) went wrong by the offset.
    /// Asserting Kind==Utc on the entity handed to the repository is the guard: if a
    /// writer ever regresses to DateTime.Now, the captured Kind is Local and this goes red.
    /// </summary>
    public class InboxServiceTimestampTests
    {
        // Captures the InboxMessage the service hands to the repository.
        private static InboxMessage Capture(Mock<IInboxRepository> repo, Action<InboxService> invoke)
        {
            InboxMessage captured = null!;
            repo.Setup(r => r.AddOrUpdate(It.IsAny<InboxMessage>()))
                .Callback<InboxMessage>(m => captured = m);
            invoke(new InboxService(repo.Object));
            return captured;
        }

        [Test]
        public void SendMessage_StampsUtc()
        {
            var repo = new Mock<IInboxRepository>();
            var msg = Capture(repo, svc => svc.SendMessage(toId: 2, fromId: 1, content: "hi"));

            Assert.That(msg.TimeStamp, Is.Not.Null, "SendMessage must set a TimeStamp");
            Assert.That(msg.TimeStamp!.Value.Kind, Is.EqualTo(DateTimeKind.Utc),
                "SendMessage must write a UTC instant, not the server's local clock");
        }

        [Test]
        public void SendToInbox_StampsUtc()
        {
            var repo = new Mock<IInboxRepository>();
            var msg = Capture(repo, svc => svc.SendToInbox(userId: 2, sender: "bot", subject: "s", content: "c"));

            Assert.That(msg.TimeStamp, Is.Not.Null, "SendToInbox must set a TimeStamp");
            Assert.That(msg.TimeStamp!.Value.Kind, Is.EqualTo(DateTimeKind.Utc),
                "SendToInbox must write a UTC instant, not the server's local clock");
        }
    }
}
