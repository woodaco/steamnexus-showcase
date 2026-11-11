using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using SteamProject.DAL.Abstract;
using SteamProject.Models;
using SteamProject.Models.DTO;
using SteamProject.Services;

namespace SteamProject.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
[AutoValidateAntiforgeryToken]
public class SteamController : ControllerBase
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IUserRepository _userRepository;
    private readonly ISteamService _steamService;
    private readonly IGameRepository _gameRepository;
    private readonly IUserGameInfoRepository _userGameInfoRepository;
    private readonly IEmailSender _emailSender;
    private readonly IFriendRepository _friendRepository;
    private readonly IInboxService _inboxService;
    private readonly IIGDBGenresRepository _iGDBGenreRepository;
    private readonly ICompetitionRepository _competitionRepository;

    public SteamController(UserManager<IdentityUser> userManager, IUserRepository userRepository, ISteamService steamService, IGameRepository gameRepository, IUserGameInfoRepository userGameInfoRepository, IEmailSender emailSender, IFriendRepository friendRepository, IInboxService inboxService, IIGDBGenresRepository iGDBGenresRepository, ICompetitionRepository competitionRepository)
    {
        _userManager = userManager;
        _userRepository = userRepository;
        _steamService = steamService;
        _gameRepository = gameRepository;
        _userGameInfoRepository = userGameInfoRepository;
        _emailSender = emailSender;
        _friendRepository = friendRepository;
        _inboxService = inboxService;
        _iGDBGenreRepository = iGDBGenresRepository;
        _competitionRepository = competitionRepository;
    }

    /// <summary>Resolves the current authenticated user's domain <see cref="User"/> row, or null.</summary>
    private User? GetCurrentUser()
    {
        var identityId = _userManager.GetUserId(User);
        return identityId is null ? null : _userRepository.GetUser(identityId);
    }

    // ---- Read endpoints: authenticated proxy of public Steam data (friend comparison etc.) ----

    [HttpGet("user")]
    public async Task<ActionResult> SteamUser(string steamid)
    {
        User user = await _steamService.GetSteamUserAsync(steamid);
        return Ok(user);
    }

    [HttpGet("achievements")]
    public async Task<ActionResult<AchievementRoot>> UserAchievements(string steamid, int appId)
    {
        return await _steamService.GetAchievementsAsync(steamid, appId);
    }

    [HttpGet("sharedMissingAchievements")]
    public async Task<ActionResult> SharedMissingAchievements(string userSteamId, string friendSteamId, int appId)
    {
        return Ok(await _steamService.GetSharedMissingAchievementsAsync(userSteamId, friendSteamId, appId));
    }

    [HttpGet("schema")]
    public async Task<ActionResult<SchemaRoot>> GameSchema(int appId)
    {
        return await _steamService.GetSchemaAsync(appId);
    }

    [HttpGet("games")]
    public async Task<ActionResult> Games(string userSteamId, int userId)
    {
        return Ok(await _steamService.GetGamesAsync(userSteamId, userId));
    }

    [HttpGet("sharedGames")]
    public async Task<ActionResult> SharedGames(string userSteamId, string friendSteamId, int userId)
    {
        return Ok(await _steamService.GetSharedGamesAsync(userSteamId, friendSteamId, userId));
    }

    [HttpGet("friends")]
    public async Task<ActionResult> SteamFriends(string steamid, int userId)
    {
        var listFriends = await _steamService.GetFriendsListAsync(steamid, userId);
        return Ok(listFriends.OrderBy(x => x.Id));
    }

    [HttpGet("friendSpecific")]
    public async Task<ActionResult> SpecificFriend(string userSteamId, int userId, string friendSteamId)
    {
        var friend = await _steamService.GetFriendSpecificAsync(userSteamId, userId, friendSteamId);
        return Ok(friend);
    }

    [HttpGet("gap")]
    public async Task<ActionResult<GAPRoot>> GlobalPercentages(int appId)
    {
        return await _steamService.GetGAPAsync(appId);
    }

    // ---- Friend management: owner is the authenticated user; each action verifies ownership ----

    private Friend? GetOwnedFriend(int friendId, User user)
    {
        var friend = _friendRepository.GetAll().FirstOrDefault(f => f.Id == friendId);
        return (friend is null || friend.RootId != user.Id) ? null : friend;
    }

    [HttpPost("removeFriend")]
    public ActionResult RemoveFriend(int friendId)
    {
        var user = GetCurrentUser();
        if (user is null) return Unauthorized();
        var friend = GetOwnedFriend(friendId, user);
        if (friend is null) return NotFound();
        _friendRepository.Delete(friend);
        return Ok();
    }

    [HttpPost("blockFriend")]
    public ActionResult BlockFriend(int friendId)
    {
        var user = GetCurrentUser();
        if (user is null) return Unauthorized();
        var friend = GetOwnedFriend(friendId, user);
        if (friend is null) return NotFound();
        friend.IsBlocked = true;
        _friendRepository.AddOrUpdate(friend);
        return Ok();
    }

    [HttpPost("unblockFriend")]
    public ActionResult UnblockFriend(int friendId)
    {
        var user = GetCurrentUser();
        if (user is null) return Unauthorized();
        var friend = GetOwnedFriend(friendId, user);
        if (friend is null) return NotFound();
        friend.IsBlocked = false;
        _friendRepository.AddOrUpdate(friend);
        return Ok();
    }

    // ---- Mutation endpoints: owner is derived from the authenticated user, never the client ----

    [HttpPost("hide")]
    public ActionResult Hide(string id)
    {
        var user = GetCurrentUser();
        if (user is null) return Unauthorized();
        if (!int.TryParse(id, out var gameId)) return BadRequest();

        var game = new UserGameInfo().GetGameByIdAndUser(gameId, _userGameInfoRepository, user.Id);
        if (game is null) return NotFound();

        game.SetHiddenStatusTrue();
        _userGameInfoRepository.AddOrUpdate(game);
        return Ok();
    }

    [HttpPost("unhide")]
    public ActionResult Unhide(string id)
    {
        var user = GetCurrentUser();
        if (user is null) return Unauthorized();
        if (!int.TryParse(id, out var gameId)) return BadRequest();

        var game = new UserGameInfo().GetGameByIdAndUser(gameId, _userGameInfoRepository, user.Id);
        if (game is null) return NotFound();

        game.SetHiddenStatusFalse();
        _userGameInfoRepository.AddOrUpdate(game);
        return Ok();
    }

    [HttpGet("refresh")]
    public ActionResult RefreshLibrary()
    {
        var routeValues = new RouteValueDictionary { { "refresh", true } };
        return RedirectToAction("Index", "Library", routeValues);
    }

    [HttpPost("follow")]
    public ActionResult ToggleFollow(string id)
    {
        var user = GetCurrentUser();
        if (user is null) return Unauthorized();
        if (id is null || !int.TryParse(id, out var appId)) return BadRequest();
        if (user.SteamId is null) return BadRequest("No linked Steam account.");

        var game = _userGameInfoRepository.GetAll()
            .Where(o => o.OwnerId == user.Id)
            .FirstOrDefault(g => g.Game.AppId == appId);
        if (game is null) return NotFound();

        game.Followed = !game.Followed;
        _userGameInfoRepository.AddOrUpdate(game);
        return Ok();
    }

    [HttpPost("unfollow")]
    public ActionResult UnFollow(string id)
    {
        var user = GetCurrentUser();
        if (user is null) return Unauthorized();
        if (id is null || !int.TryParse(id, out var appId)) return BadRequest();
        if (user.SteamId is null) return BadRequest("No linked Steam account.");

        var game = _userGameInfoRepository.GetAll()
            .Where(o => o.OwnerId == user.Id)
            .FirstOrDefault(g => g.Game.AppId == appId);
        if (game is null) return NotFound();

        game.Followed = false;
        _userGameInfoRepository.AddOrUpdate(game);
        return Ok();
    }

    [HttpGet("sendInvite")]
    public async Task<ActionResult> SendInvite(string email)
    {
        var user = GetCurrentUser();
        if (user is null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(email)) return BadRequest("Email is required.");

        var fixedEmail = email.Replace("%40", "@").Trim();
        // Basic server-side validation to avoid using the mail sender as an open relay.
        if (!System.Text.RegularExpressions.Regex.IsMatch(fixedEmail, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            return BadRequest("Invalid email address.");

        await _emailSender.SendEmailAsync(fixedEmail, "Invitation to SteamNexus", "<a>You're invited!</a>");
        return Ok();
    }

    [HttpPatch("setNickname")]
    public ActionResult SetNickname(string friendSteamId, string nickname)
    {
        var user = GetCurrentUser();
        if (user is null) return Unauthorized();

        var friend = _friendRepository.GetSpecificFriend(friendSteamId, user.Id);
        if (friend is null) return NotFound();

        friend.Nickname = nickname;
        _friendRepository.AddOrUpdate(friend);
        return Ok();
    }

    [HttpPatch("revertNickname")]
    public ActionResult RevertNickname(string friendSteamId)
    {
        var user = GetCurrentUser();
        if (user is null) return Unauthorized();

        var friend = _friendRepository.GetSpecificFriend(friendSteamId, user.Id);
        if (friend is null) return NotFound();

        friend.Nickname = null;
        _friendRepository.AddOrUpdate(friend);
        return Ok();
    }

    [HttpPost("UpdateTheme")]
    public IActionResult UpdateTheme(string theme)
    {
        var user = GetCurrentUser();
        if (user is null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(theme)) theme = "light";
        _userRepository.UpdateUserTheme(user.Id, theme);
        return Ok(new { success = true });
    }

    [HttpPost("DeleteComp")]
    public IActionResult DeleteComp(int compId)
    {
        var user = GetCurrentUser();
        if (user is null) return Unauthorized();

        var compFound = _competitionRepository.GetCompetitionById(compId);
        if (compFound is null)
            return NotFound(new { success = false, message = "Competition not found" });

        // Only the creator (or an admin) may delete a competition.
        if (compFound.CreatorId != user.Id && !User.IsInRole("admin"))
            return StatusCode(403, new { success = false, message = "You do not own this competition." });

        _competitionRepository.Delete(compFound);
        return Ok(new { success = true });
    }
}
