using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SteamProject.DAL.Abstract;
using SteamProject.Models;
using Microsoft.AspNetCore.Identity;
using SteamProject.Services;
using Microsoft.AspNetCore.Authorization;
using SteamProject.Models.DTO;
using SteamProject.ViewModels;
using System.ComponentModel.DataAnnotations;

namespace SteamProject.Controllers;

[Authorize(Roles = "admin")]
public class AdminController: Controller
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IUserRepository _userRepository;
    private readonly IBlackListRepository _blackListRepository;
    private readonly SignInManager<IdentityUser> _signInManager;
    private IFriendRepository _friendRepository;
    private IUserGameInfoRepository _userGameInfoRepository;
    private readonly ISteamService _steamService;
    private readonly IGameRepository _gameRepository;
    private readonly IIGDBGenresRepository _iGDBGenreRepository;
    private readonly SteamInfoDbContext _steamInfoDbContext;
    private readonly ILogger<AdminController> _logger;


    public AdminController(SignInManager<IdentityUser> signInManager, IGameRepository gameRepository, UserManager<IdentityUser> userManager, IUserRepository userRepository, IBlackListRepository blackListRepository, IUserGameInfoRepository userGameInfoRepository, IFriendRepository friendRepository, ISteamService steamService, IIGDBGenresRepository iGDBGenresRepository, SteamInfoDbContext steamInfoDbContext, ILogger<AdminController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _blackListRepository = blackListRepository;
        _friendRepository = friendRepository;
        _steamService = steamService;
        _userRepository = userRepository;
        _userGameInfoRepository = userGameInfoRepository;
        _gameRepository = gameRepository;
        _iGDBGenreRepository = iGDBGenresRepository;
        _steamInfoDbContext = steamInfoDbContext;
        _logger = logger;
    }

    public IActionResult Index()
    {
        // Live dashboard metrics (was a static welcome card).
        ViewData["UserCount"] = _userRepository.GetAllUsers().Count();
        ViewData["GameCount"] = _gameRepository.GetAll().Count();
        ViewData["GenreCount"] = _iGDBGenreRepository.GetAll().Count();
        ViewData["BannedCount"] = _blackListRepository.GetBlackList().Count();
        return View();
    }

    public async Task<IActionResult> ShowAllUsers()
    {
        var adminUsersVM = new List<AdminUsersVM>();

        foreach (var user in _userRepository.GetAllUsers())
        {
            var identityUser = await _userManager.FindByIdAsync(user.AspNetUserId);
            if (identityUser != null)
            {
                adminUsersVM.Add(new AdminUsersVM
                {
                    AspNetUserId = identityUser.Id,
                    SteamId = user.SteamId,
                    SteamName = user.SteamName,
                    Email = identityUser.Email
                });
            }
        }
        return View(adminUsersVM);
    }

    // Destructive: bans a user (removes their Steam login + data, blacklists the SteamId).
    // Must be POST + antiforgery so it can't be triggered by a crafted link / <img> (CSRF).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var loginProvider = "Steam";
        var providerKey = "https://steamcommunity.com/openid/id/" + id;

        var currentUser = _userRepository.GetAllUsers().FirstOrDefault(u => u.SteamId == id);
        if (currentUser == null)
            return NotFound("Unable to load the requested user.");

        IdentityUser? user = await _userManager.FindByIdAsync(currentUser.AspNetUserId);
        if (user == null)
            return NotFound("Unable to load the requested user.");

        // 1) Ban the user's DOMAIN data ATOMICALLY (clear profile, drop owned-games + friends, blacklist).
        //    All-or-nothing within the SteamInfo context — a mid-way failure can no longer leave a
        //    half-banned user (the old code did several independent SaveChanges with no transaction).
        using (var tx = _steamInfoDbContext.Database.BeginTransaction())
        {
            try
            {
                var currentUserGameInfo = _userGameInfoRepository.GetAll().Where(g => g.OwnerId == currentUser.Id).ToList();
                var friendInfo = _friendRepository.GetAll().Where(f => f.RootId == currentUser.Id).ToList();

                currentUser.SteamId = null;
                currentUser.AvatarUrl = null;
                currentUser.ProfileUrl = null;
                currentUser.SteamName = null;
                currentUser.PlayerLevel = null;
                currentUser.UserAchievements.Clear();

                foreach (var info in currentUserGameInfo)
                    _userGameInfoRepository.Delete(info);
                foreach (var friend in friendInfo)
                    _friendRepository.Delete(friend);

                _userRepository.AddOrUpdate(currentUser);

                if (!_blackListRepository.GetAll().Any(b => b.SteamId == id))
                    _blackListRepository.AddOrUpdate(new BlackList { SteamId = id });

                tx.Commit();
            }
            catch (Exception ex)
            {
                tx.Rollback();
                _logger.LogError(ex, "Failed to ban user with Steam ID {SteamId}; rolled back all domain changes.", id);
                TempData["AdminError"] = "Failed to ban the user — no changes were made.";
                return RedirectToAction(nameof(ShowAllUsers));
            }
        }

        // 2) Remove the Steam external login (a SEPARATE Identity context, so it can't share the
        //    transaction above). Best-effort: the blacklist already prevents re-linking, so a failure
        //    here only leaves an orphaned login row, not an unbanned user.
        var result = await _userManager.RemoveLoginAsync(user, loginProvider, providerKey);
        if (!result.Succeeded)
            _logger.LogWarning("Banned {SteamId} (data cleared + blacklisted) but could not remove the Steam login.", id);

        TempData["AdminMessage"] = $"User with Steam ID {id} has been banned.";
        return RedirectToAction(nameof(ShowAllUsers));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoadGames()
    {
        // Surface a distinct misconfiguration banner instead of silently rendering "0 games loaded".
        ViewBag.CuratorConfigured = _steamService.IsCuratorConfigured;

        IEnumerable<Game> games = await _steamService.GetSteamCuratorGamesAsync();
        List<Game> returnGames = new List<Game>();
        foreach(var game in games)
        {
            try
            {
                var currentGame = _gameRepository.GetGameByAppId(game.AppId);
                if (currentGame == null)
                {
                    _gameRepository.AddOrUpdate(game);
                    returnGames.Add(game);
                }
            }
            catch
            {
                // Skip a single game that fails to save rather than aborting the entire bulk load.
                continue;
            }
        }
        return View(returnGames);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoadGameInfo()
    {
        HashSet<string> genreList = new HashSet<string>();
        List<Game> gamesList = _gameRepository.GetAll().ToList();

        if (gamesList.Count < 1)
        {
            ViewBag.MyString = "The library is empty!";
            return View();
        }
        else
        {
            List<GameVM> gameVMs = new List<GameVM>();
            string tempGenreString;
            foreach(var game in gamesList)
            {
                tempGenreString = "";
                var currentGame = _gameRepository.GetGameByAppId(game.AppId);
                if (currentGame.Genres == null || currentGame.Genres == "The genres couldn't be grabbed")
                {
                    var gameVM = await _steamService.GetGameInfoAsync(game);

                    try
                    {
                        var genreResults = _steamService.ExtractGenresFromGameInfo(gameVM);
                        var categoryResults = _steamService.ExtractCategoriesFromGameInfo(gameVM);

                        foreach(var genre in genreResults)
                        {
                            tempGenreString += genre + ",";
                            genreList.Add(genre);
                        }
                        if (tempGenreString.Length > 0)
                            currentGame.Genres = tempGenreString.Substring(0, tempGenreString.Length - 1);

                        if (categoryResults.Count > 0)
                            currentGame.Categories = string.Join(",", categoryResults);
                    }
                    catch
                    {
                        // Steam API failed
                    }

                    gameVM._game = game;
                    gameVM._appId = game.AppId;
                    if(currentGame.Genres == null)
                    {
                        currentGame.Genres = "The genres couldn't be grabbed";
                    }
                    _gameRepository.AddOrUpdate(currentGame);
                    gameVMs.Add(gameVM);
                }
            }

            var genres = _iGDBGenreRepository.GetGenreList();

            foreach(var genre in genreList)
            {
                bool contains = _iGDBGenreRepository.GetGenreList().Contains(genre);
                if(!contains)
                {
                    _iGDBGenreRepository.AddOrUpdate(new Igdbgenre {
                    Name = genre
                    });
                }
            }
            return View(gameVMs);
        }
    }

    public IActionResult ViewGames()
    {
        var gameList = _gameRepository.GetAll().ToList();
        return View(gameList);
    }

    public IActionResult ViewBannedIds()
    {
        return View(_blackListRepository.GetBlackList());
    }

    // Reverses a ban: removes the SteamId from the blacklist so the user can re-link their account.
    // POST + antiforgery so it can't be triggered by a crafted link (CSRF), matching Delete/Ban.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Unban(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            TempData["AdminError"] = "No Steam ID supplied to unban.";
            return RedirectToAction(nameof(ViewBannedIds));
        }

        var removed = _blackListRepository.RemoveFromBlackList(id);
        TempData["AdminMessage"] = removed
            ? $"Steam ID {id} has been unbanned and may re-link."
            : $"Steam ID {id} was not on the blacklist.";
        return RedirectToAction(nameof(ViewBannedIds));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CleanupGenres()
    {
        int genresFixed = 0;
        int gamesFixed = 0;
        int duplicatesRemoved = 0;

        // Step 1: Clean up Games.Genres column - remove stray quotes
        var allGames = _gameRepository.GetAll().ToList();
        foreach (var game in allGames)
        {
            if (!string.IsNullOrEmpty(game.Genres) && game.Genres != "The genres couldn't be grabbed")
            {
                var originalGenres = game.Genres;
                var cleanedGenres = string.Join(",",
                    game.Genres.Split(',')
                        .Select(g => g.Trim().Trim('"', '\''))
                        .Where(g => !string.IsNullOrWhiteSpace(g))
                        .Distinct());

                if (originalGenres != cleanedGenres)
                {
                    game.Genres = cleanedGenres;
                    _gameRepository.AddOrUpdate(game);
                    gamesFixed++;
                }
            }

            // Also clean Categories
            if (!string.IsNullOrEmpty(game.Categories))
            {
                var originalCategories = game.Categories;
                var cleanedCategories = string.Join(",",
                    game.Categories.Split(',')
                        .Select(c => c.Trim().Trim('"', '\''))
                        .Where(c => !string.IsNullOrWhiteSpace(c))
                        .Distinct());

                if (originalCategories != cleanedCategories)
                {
                    game.Categories = cleanedCategories;
                    _gameRepository.AddOrUpdate(game);
                }
            }
        }

        // Step 2: Clean up Igdbgenres table - remove duplicates with quotes
        var allGenres = _iGDBGenreRepository.GetAll().ToList();
        var cleanGenreNames = new HashSet<string>();
        var genresToDelete = new List<Igdbgenre>();

        foreach (var genre in allGenres)
        {
            var cleanName = genre.Name.Trim().Trim('"', '\'');

            if (cleanName != genre.Name)
            {
                // This genre has quotes - check if clean version exists
                if (cleanGenreNames.Contains(cleanName))
                {
                    // Clean version already exists, mark this for deletion
                    genresToDelete.Add(genre);
                    duplicatesRemoved++;
                }
                else
                {
                    // Update this genre to the clean name
                    genre.Name = cleanName;
                    _iGDBGenreRepository.AddOrUpdate(genre);
                    cleanGenreNames.Add(cleanName);
                    genresFixed++;
                }
            }
            else
            {
                cleanGenreNames.Add(cleanName);
            }
        }

        // Delete duplicates
        foreach (var genre in genresToDelete)
        {
            _iGDBGenreRepository.Delete(genre);
        }

        TempData["SuccessMessage"] = $"Cleanup complete! Fixed {gamesFixed} games, {genresFixed} genre names, removed {duplicatesRemoved} duplicates.";
        return RedirectToAction(nameof(Index));
    }

    public IActionResult ViewGenres()
    {
        var genres = _iGDBGenreRepository.GetAll().OrderBy(g => g.Name).ToList();
        return View(genres);
    }
}