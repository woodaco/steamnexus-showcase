using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using SteamProject.DAL.Abstract;
using SteamProject.Models;
using SteamProject.Models.DTO;
using SteamProject.Services;
using Newtonsoft.Json.Linq;

namespace SteamProject.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
[AutoValidateAntiforgeryToken]
public class MessagesController : ControllerBase
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IUserRepository _userRepository;
    private readonly IInboxRepository _inboxRepository;
    private readonly IInboxService _inboxService;
    private readonly IFriendRepository _friendRepository;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<MessagesController> _logger;

    public MessagesController(UserManager<IdentityUser> userManager, IUserRepository userRepository, IInboxRepository inboxRepository, IInboxService inboxService, IFriendRepository friendRepository, IEmailSender emailSender, ILogger<MessagesController> logger)
    {
        _userManager = userManager;
        _userRepository = userRepository;
        _inboxRepository = inboxRepository;
        _inboxService = inboxService;
        _friendRepository = friendRepository;
        _emailSender = emailSender;
        _logger = logger;
    }

    /// <summary>Resolves the current authenticated user's domain <see cref="User"/> row, or null.</summary>
    private User? GetCurrentUser()
    {
        var identityId = _userManager.GetUserId(User);
        return identityId is null ? null : _userRepository.GetUser(identityId);
    }

    [HttpGet("messageCount")]
    public ActionResult MessageCount()
    {
        var user = GetCurrentUser();
        if (user is null) return Unauthorized();
        return Ok(_inboxRepository.GetUnreadCount(user.Id));
    }

    [HttpGet("messagesBetween")]
    public ActionResult MessagesBetween(int toId)
    {
        // The "from" side is always the authenticated user; callers may only read their own threads.
        var user = GetCurrentUser();
        if (user is null) return Unauthorized();

        List<InboxMessage> messagesBetween = _inboxRepository.GetMessagesBetween(user.Id, toId);
        var messages = new JArray();
        foreach (var message in messagesBetween)
        {
            messages.Add(new JObject
            {
                { "from", message.SenderId },
                { "to", message.RecipientId },
                { "content", message.Content },
                // Emit an ISO-8601 UTC instant (e.g. 2026-10-23T15:04:05Z) so the browser can
                // localise it to the reader's own clock and show a full date — instead of a
                // server-local "hh:mm tt" string that had no date and no timezone of record.
                // ToUniversalTime() also corrects legacy Local-kind rows written before the switch.
                { "time", message.TimeStamp.HasValue ? message.TimeStamp.Value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'") : "" }
            });
        }
        // Opening a conversation marks the messages this user received from the other party as read.
        _inboxRepository.MarkConversationRead(user.Id, toId);
        return Ok(new JObject { { "messages", messages } }.ToString());
    }

    [HttpPost("sendMessageTo")]
    public async Task<ActionResult> SendMessageTo([FromBody] SendMessageRequest req)
    {
        // Sender is derived from auth, never trusted from the client (was a spoofing vector).
        // Body (not querystring) so messages containing & or # are preserved.
        var user = GetCurrentUser();
        if (user is null) return Unauthorized();
        if (req is null || string.IsNullOrWhiteSpace(req.Message)) return BadRequest("Message cannot be empty.");

        var recipient = await _userRepository.FindByIdAsync(req.ToId);
        if (recipient is null) return NotFound("Recipient does not exist.");

        await _inboxService.SendMessageAsync(req.ToId, user.Id, req.Message);

        // Best-effort email notification (the in-app inbox is the source of truth; only user->user
        // messages notify, never the system bot). A mail failure must not fail the message send.
        await NotifyRecipientByEmailAsync(recipient, user);

        return Ok();
    }

    private async Task NotifyRecipientByEmailAsync(User recipient, User sender)
    {
        string? email = null;
        try
        {
            if (recipient.AspNetUserId is null) return;
            var identity = await _userManager.FindByIdAsync(recipient.AspNetUserId);
            email = identity?.Email;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not resolve the recipient email for a message notification.");
            return;
        }

        if (string.IsNullOrEmpty(email)) return;

        var from = string.IsNullOrEmpty(sender.SteamName) ? "a friend" : sender.SteamName;
        var body = $"You have a new message from {System.Net.WebUtility.HtmlEncode(from)} on SteamNexus. Log in to read and reply.";

        // Fire-and-forget the actual send so a slow/undeliverable SMTP call can never hang (or slow) the
        // message-send response. EmailSender has no scoped/request-bound state, so it is safe post-request.
        _ = Task.Run(async () =>
        {
            try { await _emailSender.SendEmailAsync(email, "You have a new SteamNexus message", body); }
            catch (Exception ex) { _logger.LogWarning(ex, "Background message-notification email failed (message was delivered in-app)."); }
        });
    }

    [HttpGet("userContacts")]
    public ActionResult VerifiedContacts()
    {
        var user = GetCurrentUser();
        if (user is null) return Unauthorized();

        var contacts = new JArray();
        List<Friend> friends = _friendRepository.GetFriends(user.Id);
        var friendSteamIds = friends
            .Where(f => f.SteamId is not null)
            .Select(f => f.SteamId)
            .Distinct()
            .ToList();
        var userById = _userRepository.GetUsersBySteamId(friendSteamIds)
            .ToDictionary(u => u.SteamId, u => u.Id);
        foreach (var friend in friends)
        {
            if (friend.SteamId is not null && userById.TryGetValue(friend.SteamId, out var correctId))
            {
                contacts.Add(new JObject
                {
                    { "id", correctId },
                    { "avatar", friend.AvatarFullUrl },
                    { "name", friend.SteamName },
                    { "nickname", friend.Nickname }
                });
            }
        }
        return Ok(new JObject { { "contacts", contacts } }.ToString());
    }
}
