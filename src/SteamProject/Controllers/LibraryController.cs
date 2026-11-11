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

public class LibraryController: Controller
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IUserRepository _userRepository;
    private readonly IGameRepository _gameRepository;
    private readonly ISteamService _steamService;
    private readonly IUserGameInfoRepository _userGameInfoRepository;
    private readonly IIGDBGenresRepository _iGDBGenreRepository;

    public LibraryController(UserManager<IdentityUser> userManager, IUserRepository userRepository, IGameRepository gameRepository, IUserGameInfoRepository userGameInfoRepository, ISteamService steamService, IIGDBGenresRepository iGDBGenresRepository)
    {
        _userManager = userManager;
        _userRepository = userRepository;
        _gameRepository = gameRepository;
        _steamService = steamService;
        _userGameInfoRepository = userGameInfoRepository;
        _iGDBGenreRepository = iGDBGenresRepository;
    }

    [Authorize]
    public async Task<IActionResult> Index(bool refresh)
    {
        // Going to be used to populate filter dropdowns
        HashSet<string> genreList = new HashSet<string>();
        HashSet<string> categoryList = new HashSet<string>();
        string? id = _userManager.GetUserId(User);

        var userLibraryVM = new UserLibraryVM();

        if(id is null)
        {
            return View();
        }
        else
        {
            User user = _userRepository.GetUser(id);

            if (user.AvatarUrl == null && user.SteamId != null)
            {
                // Fetch user data and level in parallel
                var steamUserTask = _steamService.GetSteamUserAsync(user.SteamId);
                var steamLevelTask = _steamService.GetUserLevelAsync(user.SteamId);

                await Task.WhenAll(steamUserTask, steamLevelTask);

                var steamUser = await steamUserTask;
                user.SteamName = steamUser.SteamName;
                user.AvatarUrl = steamUser.AvatarUrl;
                user.PlayerLevel = await steamLevelTask;

                _userRepository.AddOrUpdate(user);
            }

            userLibraryVM._user = user;
            List<UserGameInfo> gameInfo = _userGameInfoRepository.GetAllUserGameInfo(user.Id);
            userLibraryVM._games = new HashSet<Game>();
            UserGameInfo? currentUserInfo = new UserGameInfo();

            if (gameInfo.Count == 0)
            {
                refresh = true;
            }
            if (user.SteamId == null)
            {
                return View();
            }
            if(refresh == true)
            {
                List<Game>? games = (await _steamService.GetGamesAsync(user.SteamId, user.Id)).ToList();
                
                if(games.Count == 0)
                    return View();

                // Checks to see  if each individual game from Steam is in our db or not on a library refresh
                // --- Refresh isn't the page refreshing, it's the user initiating a refresh to get newly added games
                foreach(var game in games)
                {
                    try
                    {
                        var currentGame = _gameRepository.GetGameByAppId(game.AppId);
                        var tempGenreString = "";
                        if(currentGame != null && currentGame.Genres == null)
                        {
                            try
                            {
                                var gameVM = await _steamService.GetGameInfoAsync(currentGame);
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
                                {
                                    currentGame.Categories = string.Join(",", categoryResults);
                                    foreach(var category in categoryResults)
                                        categoryList.Add(category);
                                }
                            }
                            catch
                            {
                                // Steam API failed
                            }

                            if(currentGame.Genres == null)
                            {
                                currentGame.Genres = "The genres couldn't be grabbed";
                            }
                        }

                        int? playTime = game.PlayTime;
                        int? lastPlayed = game.LastPlayed;

                        game.PlayTime = 0;
                        game.LastPlayed = 0;

                        try
                        {
                            currentUserInfo = _userGameInfoRepository.GetUserInfoForGame( game.AppId, user.Id );
                        }
                        catch
                        {
                            currentUserInfo = null;
                        }

                        //Check if game is in database, if not add it
                        if (currentGame == null)
                        {
                            if(refresh)
                            {
                                try
                                {
                                    var gameVM = await _steamService.GetGameInfoAsync(game);
                                    var genreResults = _steamService.ExtractGenresFromGameInfo(gameVM);
                                    var categoryResults = _steamService.ExtractCategoriesFromGameInfo(gameVM);

                                    foreach(var genre in genreResults)
                                    {
                                        tempGenreString += genre + ",";
                                        genreList.Add(genre);
                                    }
                                    if (tempGenreString.Length > 0)
                                        game.Genres = tempGenreString.Substring(0, tempGenreString.Length - 1);

                                    if (categoryResults.Count > 0)
                                    {
                                        game.Categories = string.Join(",", categoryResults);
                                        foreach(var category in categoryResults)
                                            categoryList.Add(category);
                                    }
                                }
                                catch
                                {
                                    // Steam API failed
                                }

                                if(game.Genres == null)
                                {
                                    game.Genres = "The genres couldn't be grabbed";
                                }
                                _gameRepository.AddOrUpdate(game);
                            }

                            var temp = _gameRepository.GetAll(g => g.AppId == game.AppId).FirstOrDefault();
                            if (currentUserInfo == null)
                            {
                                _userGameInfoRepository.AddOrUpdate(new UserGameInfo{
                                    OwnerId = user.Id,
                                    GameId = temp.Id,
                                    PlayTime = playTime,
                                    LastPlayed = lastPlayed,
                                    Hidden = false,
                                    Followed = false,
                                    Game = game,
                                    Owner = user
                                });
                                userLibraryVM._games.Add(game);
                            }
                            else
                            {
                                UserGameInfo currentGameInfo = gameInfo.Single(g => g.GameId == temp.Id);
                                currentGameInfo.LastPlayed = lastPlayed;
                                currentGameInfo.PlayTime = playTime;
                                _userGameInfoRepository.AddOrUpdate(currentGameInfo);
                                userLibraryVM._games.Add(game);
                            }
                        }
                        else
                        {
                            if (currentUserInfo == null)
                            {

                                UserGameInfo newInfo = new UserGameInfo
                                {
                                    OwnerId = user.Id,
                                    GameId = currentGame.Id,
                                    PlayTime = playTime,
                                    LastPlayed = lastPlayed,
                                    Hidden = false,
                                    Followed = false,
                                    Owner = user,
                                    Game = currentGame
                                };
                                _userGameInfoRepository.AddOrUpdate(newInfo);
                                userLibraryVM._games.Add(game);

                            }
                            else
                            {
                                UserGameInfo currentGameInfo = gameInfo.Single(g => g.GameId == currentGame.Id);
                                currentGameInfo.LastPlayed = lastPlayed;
                                currentGameInfo.PlayTime = playTime;
                                _userGameInfoRepository.AddOrUpdate(currentGameInfo);
                                userLibraryVM._games.Add(game);
                            }
                        }
                    }
                    catch
                    {
                        throw new Exception("Current game couldn't be saved to the db!" + game.Name);
                    }
                }
            }
            else
            {
                userLibraryVM._games = _gameRepository.GetGamesListByUserInfo(gameInfo);

                // Extract genres and categories from existing games
                foreach(var game in userLibraryVM._games)
                {
                    if (!string.IsNullOrEmpty(game.Genres) && game.Genres != "The genres couldn't be grabbed")
                    {
                        foreach(var genre in game.Genres.Split(','))
                        {
                            // Clean up any stray quotes
                            var cleanGenre = genre.Trim().Trim('"', '\'');
                            if (!string.IsNullOrWhiteSpace(cleanGenre))
                                genreList.Add(cleanGenre);
                        }
                    }
                    if (!string.IsNullOrEmpty(game.Categories))
                    {
                        foreach(var category in game.Categories.Split(','))
                        {
                            // Clean up any stray quotes
                            var cleanCategory = category.Trim().Trim('"', '\'');
                            if (!string.IsNullOrWhiteSpace(cleanCategory))
                                categoryList.Add(cleanCategory);
                        }
                    }
                }
            }

            // Populate genres for filter
            userLibraryVM._genres = new HashSet<string>();
            if(genreList.Count() == 0)
            {
                var genres = _iGDBGenreRepository.GetGenreList().OrderBy(s => s);
                foreach(var genre in genres)
                {
                    // Clean up any stray quotes from the database
                    var cleanGenre = genre.Trim().Trim('"', '\'');
                    if (!string.IsNullOrWhiteSpace(cleanGenre))
                        userLibraryVM._genres.Add(cleanGenre);
                }
            }
            else
            {
                foreach(var genre in genreList.OrderBy(g => g))
                {
                    userLibraryVM._genres.Add(genre);
                    // Add new genres to IGDB table for future use
                    if(!_iGDBGenreRepository.GetGenreList().Contains(genre))
                    {
                        _iGDBGenreRepository.AddOrUpdate(new Igdbgenre { Name = genre });
                    }
                }
            }

            // Populate categories for filter
            userLibraryVM._categories = new HashSet<string>();
            foreach(var category in categoryList.OrderBy(c => c))
                userLibraryVM._categories.Add(category);

            userLibraryVM._user.UserGameInfos = userLibraryVM._user.UserGameInfos.OrderBy(g => g.Game.Name).ToList();
            return View(userLibraryVM);
        }
    }
    public async Task<IActionResult> ShowMoreInfo(int appId)
    {
        string? id = _userManager.GetUserId(User);
        if (id is null) return Unauthorized();
        User user = _userRepository.GetUser(id);
        if (user is null) return Unauthorized();

        Game game = _gameRepository.GetGameByAppId(appId);
        if (game is null) return NotFound();

        GameVM gameVM = await _steamService.GetGameInfoAsync(game);

        gameVM._game = game;
        gameVM._appId = appId;
        // Only the CURRENT user's ownership row — was unfiltered, leaking another user's playtime (IDOR).
        gameVM._userGame = _userGameInfoRepository.GetAll(g => g.GameId == game.Id && g.OwnerId == user.Id).FirstOrDefault();
        
        try
        {
            gameVM.playTime = Math.Round(Convert.ToDouble(gameVM._userGame.PlayTime)/60, 1);
        }
        catch
        {
            gameVM.playTime = 0;
        }

        gameVM.cleanRequirements();
        gameVM.cleanDescriptions();

        return View(gameVM);
    }


    public IActionResult Recommendations()
    {
        string? id = _userManager.GetUserId(User);
        if (id is null) return Unauthorized();
        User? user = _userRepository.GetUser(id);
        if (user is null) return Unauthorized();

        // This is the dictionary that will hold the scores for each game in the db.
        List<Dictionary<Game, int>> recommendationList = new List<Dictionary<Game, int>>();

        // List of current users games.
        List<UserGameInfo>? userGameList = _userGameInfoRepository.GetAllUserGameInfo(user.Id);

        // Simple hashset of game id's to check and see what games a user doesn't own.
        HashSet<int> userGameIds = new HashSet<int>();

        // Setting up dictionaries for scoring genres and categories.
        Dictionary<string, int> genreScores = new Dictionary<string, int>();
        Dictionary<string, int> categoryScores = new Dictionary<string, int>();

        foreach (var genre in _iGDBGenreRepository.GetGenreList())
        {
            if (!genreScores.ContainsKey(genre))
                genreScores[genre] = 0;
        }

        // Looks through each game a user has and scores genres and categories.
        foreach(var game in userGameList)
        {
            var temp = _gameRepository.FindById(game.GameId);
            userGameIds.Add(temp.AppId);

            // Score genres
            if (!string.IsNullOrEmpty(temp.Genres))
            {
                foreach(var genre in temp.Genres.Split(","))
                    {
                        var trimmedGenre = genre.Trim();
                        if (genreScores.ContainsKey(trimmedGenre))
                            genreScores[trimmedGenre] += 1;
                        else
                            genreScores[trimmedGenre] = 1;
                    }
                }

                // Score categories (multiplayer, achievements, controller support, etc.)
                if (!string.IsNullOrEmpty(temp.Categories))
                {
                    foreach(var category in temp.Categories.Split(","))
                    {
                        var trimmedCategory = category.Trim();
                        if (categoryScores.ContainsKey(trimmedCategory))
                            categoryScores[trimmedCategory] += 1;
                        else
                            categoryScores[trimmedCategory] = 1;
                    }
                }
            }

            // Gets all of the games from the DB and sets them up to be scored.
            foreach(var game in _gameRepository.GetAll().ToList())
            {
                if(!userGameIds.Contains(game.AppId))
                {
                    recommendationList.Add(new Dictionary<Game, int>
                    {
                        {game, 0}
                    });
                }
            }

            // Score each potential recommendation based on genre and category matches.
            foreach(var dictionary in recommendationList)
            {
                var game = dictionary.First().Key;
                int score = 0;

                // Add genre scores
                if (!string.IsNullOrEmpty(game.Genres))
                {
                    foreach(var genre in game.Genres.Split(","))
                    {
                        var trimmedGenre = genre.Trim();
                        if (genreScores.ContainsKey(trimmedGenre))
                            score += genreScores[trimmedGenre];
                    }
                }

                // Add category scores (weighted slightly less than genres)
                if (!string.IsNullOrEmpty(game.Categories))
                {
                    foreach(var category in game.Categories.Split(","))
                    {
                        var trimmedCategory = category.Trim();
                        if (categoryScores.ContainsKey(trimmedCategory))
                            score += categoryScores[trimmedCategory];
                    }
                }

                dictionary[game] = score;
            }

        // Order by score descending and cap the output to the top 30 (a recommendations page never needs
        // the entire scored catalog shipped to the view).
        var orderedRecommendationList = recommendationList
            .OrderByDescending(dict => dict.Values.Sum())
            .Take(30)
            .ToList();

        RecommendationVM model = new RecommendationVM {scoredGames = orderedRecommendationList, _user = user};

        return View(model);
    }
}