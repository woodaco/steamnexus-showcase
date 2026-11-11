using SteamProject.Models;

namespace SteamProject.DAL.Abstract;

public interface IInboxRepository : IRepository<InboxMessage>
{
    List<InboxMessage> GetInboxMessages(int userId);
    List<InboxMessage> GetMessagesBetween(int fromId, int toId);

    /// <summary>Count of unread messages received by the user.</summary>
    int GetUnreadCount(int userId);

    /// <summary>Marks all messages the user received from <paramref name="senderId"/> as read.</summary>
    void MarkConversationRead(int recipientId, int senderId);
}