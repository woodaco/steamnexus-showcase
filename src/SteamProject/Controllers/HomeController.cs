using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SteamProject.DAL.Abstract;
using SteamProject.Models;
using SteamProject.Helpers;
using SteamProject.Models.DTO;
using SteamProject.Services;
using SteamProject.ViewModels;
using SteamProject.DAL.Concrete;

namespace SteamProject.Controllers;

public class HomeController : Controller
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IUserRepository _userRepository;
    private readonly IGameRepository _gameRepository;
    private readonly IUserGameInfoRepository _userGameInfoRepository;
    private readonly ISteamService _steamService;
    private readonly IFriendRepository _friendRepository;
    private readonly IInboxService _inboxService;
    private readonly ICompetitionRepository _competitionRepository;

    public HomeController(
        UserManager<IdentityUser> userManager,
        IUserRepository userRepository,
        IGameRepository gameRepository,
        IUserGameInfoRepository userGameInfoRepository,
        ISteamService steamService,
        IFriendRepository friendRepository,
        IInboxService inboxService,
        ICompetitionRepository competitionRepository
    )
    {
        _userManager = userManager;
        _userRepository = userRepository;
        _gameRepository = gameRepository;
        _steamService = steamService;
        _userGameInfoRepository = userGameInfoRepository;
        _friendRepository = friendRepository;
        _inboxService = inboxService;
        _competitionRepository = competitionRepository;
    }

    [AllowAnonymous]
    public IActionResult Index()
    {
        return View();
    }

    [Authorize]
    public async Task<IActionResult> Dashboard()
    {
        string? id = _userManager.GetUserId(User);

        if (id is null)
        {
            return View();
        }
        else
        {
            UserDashboardVM dashboardVm = new UserDashboardVM();
            User user = _userRepository.GetUser(id);

            if (user == null)
            {
                return View();
            }

            dashboardVm.User = user;

            // Initialize collections to empty lists to prevent null reference exceptions in the view
            dashboardVm.RecentGames = new List<List<Game>>();
            dashboardVm.FollowedGames = new List<List<Game>>();
            dashboardVm.CurrentCompetitions = new List<Competition>();
            dashboardVm.PreviousCompetitions = new List<List<Competition>>();

            if (user.SteamId != null)
            {
                //get list of userinfo ordered by last played
                List<UserGameInfo> currentUserInfo = _userGameInfoRepository.GetAllUserGameInfo(user.Id)
                    .OrderByDescending(u => u.LastPlayed).ToList();

                //get games list for user
                List<Game>? games = _gameRepository.GetGamesListByUserInfo(currentUserInfo).Take(6).ToList();

                HashSet<Game> followedGames =
                    _gameRepository.GetGamesListByUserInfo(currentUserInfo.Where(u => u.Followed).ToList());

                // Add games and followed games to the dashboardVm using helper function
                dashboardVm.RecentGames = HelperMethods.SplitListIntoChunks(games, 3);
                dashboardVm.FollowedGames = HelperMethods.SplitListIntoChunks(followedGames.ToList(), 3);

                // Get the user's competitions
                dashboardVm.CurrentCompetitions = _competitionRepository.GetCurrentCompetitionsBySteamId(user.SteamId);

                List<Competition> previousCompetitions = _competitionRepository.GetPreviousCompetitionsBySteamId(user.SteamId);
                dashboardVm.PreviousCompetitions = HelperMethods.SplitListIntoChunks(previousCompetitions, 3);
            }

            return View(dashboardVm);
        }
    }

    public async Task<IActionResult> ShowMoreNews(int appId)
    {
        string? id = _userManager.GetUserId(User);

        if (id != null)
        {
            User user = _userRepository.GetUser(id);

            Game game = _gameRepository.GetGameByAppId(appId);

            if (game != null)
            {
                GameNewsVM gameNewsVM = await _steamService.GetGameNewsAsync(game);
                gameNewsVM._game = game;
                return View(gameNewsVM);

            }
            return View();

        }
        else
        {
            return View();
        }
    }

    [AllowAnonymous]
    public IActionResult Privacy()
    {
        return View();
    }

    /// <summary>
    /// Friendly page for unknown URLs. UseStatusCodePagesWithReExecute sends bare 4xx/5xx page
    /// responses here with <paramref name="code"/> set to the original status, which is kept.
    /// Named PageNotFound (routed as Home/NotFound) so it does not hide ControllerBase.NotFound().
    /// </summary>
    [AllowAnonymous]
    [ActionName("NotFound")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult PageNotFound(int? code = null)
    {
        int status = code is >= 400 and <= 599 ? code.Value : StatusCodes.Status404NotFound;
        Response.StatusCode = status;
        ViewData["StatusCode"] = status;
        return View("NotFound");
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    [Authorize]
    public async Task<IActionResult> Friends()
    {
        string? id = _userManager.GetUserId(User);
        if (id is null)
        {
            return View();
        }
        User user = _userRepository.GetUser(id);

        if (user == null)
        {
            return View();
        }

        if (!user.Friends.Any())
        {
            var Friends = _friendRepository.GetFriends(user.Id);
            if (Friends.Count() == 0 && user.SteamId != null)
            {
                Friends = await _steamService.GetFriendsListAsync(user.SteamId, user.Id);
                foreach (var newFriend in Friends)
                {
                    _friendRepository.AddOrUpdate(newFriend);
                }
            }
        }

        var allFriends = _friendRepository.GetFriends(user.Id);
        List<Friend> friends = allFriends.Where(f => !f.IsBlocked).ToList();
        List<Friend> blocked = allFriends.Where(f => f.IsBlocked).ToList();
        // Materialise once (HashSet) so the per-friend check below is O(1) instead of
        // re-enumerating GetAllUsers() for every friend.
        var steamIds = _userRepository.GetAllUsers().Select(u => u.SteamId).ToHashSet();

        foreach (var friend in friends)
        {
            if (steamIds.Contains(friend.SteamId)) {
                friend.Linked = true;
            }
        }

        FriendsPageVM vm = new(friends, user.Id, user.SteamId) { Blocked = blocked };
        return View(vm);
    }
}
