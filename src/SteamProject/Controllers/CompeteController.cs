
using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SteamProject.DAL.Abstract;
using SteamProject.DAL.Concrete;
using SteamProject.Models;
using SteamProject.Models.DTO;
using SteamProject.Services;
using SteamProject.ViewModels;

namespace SteamProject.Controllers;

// Validate antiforgery on all mutating actions (Create/Initiate POST were CSRF-vulnerable). The
// Create form and the JS already emit the token; GET actions are unaffected.
[AutoValidateAntiforgeryToken]
public class CompeteController : Controller
{
    private readonly ILogger<CompeteController> _logger;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IUserRepository _userRepository;
    private readonly IGameRepository _gameRepository;
    private readonly IUserGameInfoRepository _userGameInfoRepository;
    private readonly IFriendRepository _friendRepository;
    private readonly IGameAchievementRepository _gameAchievementRepository;
    private readonly IUserAchievementRepository _userAchievementRepository;
    private readonly ICompetitionRepository _competitionRepository;
    private readonly ICompetitionPlayerRepository _competitionPlayerRepository;
    private readonly ICompetitionGameAchievementRepository _competitionGameAchievementRepository;
    private readonly ISteamService _steamService;
    private readonly IInboxService _inboxService;
    private readonly IStatusRepository _statusRepository;
    private readonly ICompetitionVoteRepository _competitionVoteRepository;

    public CompeteController(
        ILogger<CompeteController> logger
        ,ISteamService steamService
        , IUserRepository userRepository
        , IFriendRepository friendRepository
        , IGameAchievementRepository gameAchievementRepository
        , IUserAchievementRepository userAchievementRepository
        , IGameRepository gameRepository
        , IUserGameInfoRepository userGameInfoRepository
        , ICompetitionRepository competitionRepository
        , ICompetitionPlayerRepository competitionPlayerRepository
        , ICompetitionGameAchievementRepository competitionGameAchievementRepository
        , UserManager<IdentityUser> userManager
        , IInboxService inboxService
        , IStatusRepository statusRepository
        , ICompetitionVoteRepository competitionVoteRepository
        )
    {
        _logger = logger;
        _steamService = steamService;
        _userRepository = userRepository;
        _gameRepository = gameRepository;
        _userGameInfoRepository = userGameInfoRepository;
        _friendRepository = friendRepository;
        _gameAchievementRepository = gameAchievementRepository;
        _userAchievementRepository = userAchievementRepository;
        _competitionRepository = competitionRepository;
        _competitionPlayerRepository = competitionPlayerRepository;
        _competitionGameAchievementRepository = competitionGameAchievementRepository;
        _userManager = userManager;
        _inboxService = inboxService;
        _statusRepository = statusRepository;
        _competitionVoteRepository = competitionVoteRepository;
    }


    [Authorize]
    [HttpGet]
    public IActionResult Index()
    {
        var me = CurrentDomainUser();
        if ( me is null ) return RedirectToAction( nameof( Index ) );
        var mySteamId = me.SteamId;

        var myEntries = new List<CompetitionPlayer>();
        myEntries = _competitionPlayerRepository.GetCompetitionIdsBySteamId( mySteamId );

        var viewModel = new CompeteIndexVM();
        viewModel.Competitions = _competitionRepository.GetAllCompetitionsForUser( myEntries );
        viewModel.CompsComplete = new List<Competition>();
        viewModel.CompsRunning = new List<Competition>();

        if ( viewModel.Competitions != null )
        {
            foreach (var competition in viewModel.Competitions)
            {
                competition.Game = _gameRepository.GetGameById(competition.GameId);
                if( DateTime.Compare( competition.EndDate, DateTime.Now ) <= 0 )
                    viewModel.CompsComplete.Add( competition );
                else
                    viewModel.CompsRunning.Add( competition );
            }
        }
        
        return View( viewModel );
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Details( int compId )
    {
        var me = CurrentDomainUser();
        if ( me is null ) return RedirectToAction( nameof( Index ) );
        int CurrentUserId = me.Id;

        var viewModel = new CompeteDetailsVM();
        var competitionIn = new Competition();

        viewModel.CurrentUserId = CurrentUserId;

        competitionIn = _competitionRepository.GetCompetitionById( compId );

        if( competitionIn != null )
        {
            var gameAssociated = new Game();
            gameAssociated = _gameRepository.GetGameById( competitionIn.GameId );


            var compPlayersList = new List<CompetitionPlayer>();
            compPlayersList = _competitionPlayerRepository.GetAllForCompetition( compId );


            // List of steamids of competition's associated steam users. Feeds into GetManyUsers function.
            var idList = new List<string>();
            foreach( var cPlayer in compPlayersList )
            {
                idList.Add( cPlayer.SteamId );
            }
            var userList = new List<User>();
            userList = await _steamService.GetManyUsersAsync( idList );
            var localPlayers = _userRepository.GetAll().Where( u => u.SteamId != null && idList.Contains( u.SteamId ) ).ToList();
            SteamService.FillMissingProfilesFromLocal( userList, localPlayers );


            var compAchievements = new List<CompetitionGameAchievement>();
            compAchievements = _competitionGameAchievementRepository.GetByCompetitionId( compId );

            var gapResult = await _steamService.GetGAPAsync( competitionIn.Game.AppId );
            var percentages = gapResult?.achievementpercentages?.achievements ?? new List<GlobalAchievement>();
            
            var gameAchievements = new List<GameAchievement>();
            foreach( var ach in compAchievements )
            {

                var achievementFound = new GameAchievement();
                achievementFound = _gameAchievementRepository.GetAll().Where( gAch => gAch.Id == ach.GameAchievementId ).FirstOrDefault();

                if( achievementFound != null )
                    gameAchievements.Add( achievementFound );
            }

            var pointProcessor = new GapProcessor();
            foreach( var gameAch in gameAchievements )
            {
                foreach( var percent in percentages )
                {
                    if( gameAch.ApiName == percent.name )
                    {
                        gameAch.PointVal = pointProcessor.HandlePercent( percent.percent );
                    }
                }
            }
            

            // Participant achievement grabbing
            var userAchDict = new Dictionary<UserAchievement, User>();
            foreach( var participant in userList )
            {
                var ListIntoDict = new List<UserAchievement>();

                var userResponse = new AchievementRoot();
                userResponse = await _steamService.GetAchievementsAsync( participant.SteamId, gameAssociated.AppId );

                if( userResponse != null )
                    foreach( var ach in gameAchievements )
                    {
                        var userAchOut = new UserAchievement();
                        userAchOut = userAchOut.GetUserAchievementFromAPICall( ach, userResponse.playerstats.achievements );

                        if( userAchOut != null  && userAchOut.Achieved == true && userAchOut.AchievedWithinWindow( competitionIn ))
                            ListIntoDict.Add( userAchOut );
                    }

                foreach( var achievement in ListIntoDict )
                {
                    userAchDict.Add( achievement, participant );
                }
            }

            var userAchList = new List<KeyValuePair<UserAchievement, User>>();
            userAchList = userAchDict.OrderByDescending( p => p.Key.UnlockTime ).ToList<KeyValuePair<UserAchievement, User>>();

            var userScoreList = new List<KeyValuePair<User, CompetitionPlayer>>();
            foreach( var player in compPlayersList )
            {
                foreach( var user in userList )
                {
                    if( player.SteamId == user.SteamId )
                        userScoreList.Add( new (user, player) );
                }
            }

            
            userScoreList = userScoreList.OrderByDescending( i => i.Value.Score ).ThenBy( i => i.Key.SteamName ).ToList<KeyValuePair<User, CompetitionPlayer>>();

            userList.Clear();
            foreach( var us in userScoreList )
            {
                userList.Add( us.Key );
                foreach( var ua in userAchList )
                {
                    if( us.Key == ua.Value )
                    {
                        us.Value.Score += ua.Key.Achievement.PointVal;
                    }
                }
            }

            if (DateTime.UtcNow >= competitionIn.EndDate && competitionIn.Status.Name != "Ended")
            {
                var endedStatus = _statusRepository.GetStatusByName("Ended");
                if (endedStatus != null)
                {
                    competitionIn.Status = endedStatus;
                    _competitionRepository.AddOrUpdate(competitionIn);
                }
            }


            viewModel.CurrentComp = competitionIn;
            viewModel.Game = gameAssociated;
            viewModel.CompPlayers = compPlayersList;
            viewModel.Players = userList;
            viewModel.CompGameAchList = compAchievements;
            viewModel.GameAchList = gameAchievements;
            viewModel.Tracking = userAchList;
            viewModel.Scoreboard = userScoreList;

            // Re-match voting tally (from CompetitionVote). Majority = more than half the players want a rematch.
            var votes = _competitionVoteRepository.GetAll().Where( v => v.CompetitionId == compId ).ToList();
            viewModel.ParticipantCount = compPlayersList.Count;
            viewModel.PlayAgainVoteCount = votes.Count( v => v.WantsToPlayAgain );
            viewModel.HasUserVotedToPlayAgain = votes.Any( v => v.UserId == CurrentUserId && v.WantsToPlayAgain );
            viewModel.HasMajorityVotedToPlayAgain = viewModel.ParticipantCount > 0
                && viewModel.PlayAgainVoteCount > viewModel.ParticipantCount / 2.0;
        }

        return View( viewModel );
    }

    [Authorize]
    [HttpGet]
    public IActionResult Create()
    {
        string id = _userManager.GetUserId(User);

        var currentUser = new SteamProject.Models.User();
        currentUser = _userRepository.GetUser(id);

        var viewModel = new CompeteCreateVM();
        viewModel.SteamId = currentUser.SteamId;
        viewModel.CurrentUserId = currentUser.Id;

        return View( viewModel );
    }

    /// <summary>Resolves the current authenticated user's domain <see cref="User"/> row, or null.</summary>
    private User? CurrentDomainUser()
    {
        var authId = _userManager.GetUserId( User );
        return authId is null ? null : _userRepository.GetUser( authId );
    }

    // ---- DB-backed data feeds for the Compete/Create Duel flow ----
    // These source the user's OWN synced data (friends, owned games, achievements) from the database so a
    // competition can be created deterministically, instead of making live, non-deterministic Steam calls
    // for every keystroke. (Steam stays the lazy backfill that populates these tables elsewhere.)

    /// <summary>The current user's linked, non-blocked friends (duel opponents).</summary>
    // Attribute route so it isn't swallowed by the conventional catch-all "Compete/{friendSteamId?}/{appId?}".
    [Authorize]
    [HttpGet("/Compete/FriendsList")]
    public IActionResult FriendsList()
    {
        var me = CurrentDomainUser();
        if ( me is null ) return Unauthorized();

        var friends = _friendRepository.GetFriends( me.Id )
            .Where( f => !f.IsBlocked && f.SteamId != null )
            .OrderBy( f => f.SteamName )
            .Select( f => new { steamId = f.SteamId, steamName = f.SteamName } )
            .ToList();
        return Json( friends );
    }

    /// <summary>The current user's own synced library (for the Free-For-All create flow).</summary>
    [Authorize]
    [HttpGet("/Compete/MyGames")]
    public IActionResult MyGames()
    {
        var me = CurrentDomainUser();
        if ( me is null ) return Unauthorized();

        var games = _userGameInfoRepository.GetAllUserGameInfo( me.Id )
            .Select( g => g.GameId )
            .Distinct()
            .Select( gameId => _gameRepository.GetGameById( gameId ) )
            .Where( g => g != null )
            .Select( g => new { appId = g!.AppId, name = g.Name } )
            .OrderBy( g => g.name )
            .ToList();
        return Json( games );
    }

    /// <summary>Games BOTH the current user and the given friend own (intersection of synced libraries).</summary>
    [Authorize]
    [HttpGet("/Compete/SharedGames")]
    public IActionResult SharedGames( string friendSteamId )
    {
        var me = CurrentDomainUser();
        if ( me is null ) return Unauthorized();
        if ( string.IsNullOrWhiteSpace( friendSteamId ) ) return Json( Array.Empty<object>() );

        // The opponent must be a registered user with a synced library for shared games to exist.
        var friend = _userRepository.GetAllUsers().FirstOrDefault( u => u.SteamId == friendSteamId );
        if ( friend is null ) return Json( Array.Empty<object>() );

        var myGameIds = _userGameInfoRepository.GetAllUserGameInfo( me.Id ).Select( g => g.GameId ).ToHashSet();
        var theirGameIds = _userGameInfoRepository.GetAllUserGameInfo( friend.Id ).Select( g => g.GameId ).ToHashSet();
        myGameIds.IntersectWith( theirGameIds );

        var games = myGameIds
            .Select( gameId => _gameRepository.GetGameById( gameId ) )
            .Where( g => g != null )
            .Select( g => new { appId = g!.AppId, name = g.Name } )
            .OrderBy( g => g.name )
            .ToList();
        return Json( games );
    }

    /// <summary>The selected game's achievements to compete over (from the synced GameAchievement table).</summary>
    [Authorize]
    [HttpGet("/Compete/GameAchievements")]
    public IActionResult GameAchievements( int appId )
    {
        var me = CurrentDomainUser();
        if ( me is null ) return Unauthorized();

        var game = _gameRepository.GetGameByAppId( appId );
        if ( game is null ) return Json( Array.Empty<object>() );

        var achievements = _gameAchievementRepository.GetAchievementsFromGameId( game.Id )
            .Where( a => a.DisplayName != null )
            .OrderBy( a => a.DisplayName )
            // name is both the label and the value the Create POST matches on (GameAchievement.DisplayName).
            .Select( a => new { name = a.DisplayName, description = "" } )
            .ToList();
        return Json( achievements );
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Initiate( string friendSteamId, int appId )
    {
        var id = _userManager.GetUserId( User );
        var me = _userRepository.GetUser( id );
        // Guard bad input (this is the catch-all /Compete/{friendSteamId}/{appId} route, so it also
        // receives unknown paths). Without this, appId=0 -> GetGameByAppId(0) is null -> NRE -> 500.
        if ( me is null || string.IsNullOrWhiteSpace( friendSteamId ) || appId <= 0 )
            return RedirectToAction( nameof( Index ) );

        var mySteamId = me.SteamId;
        var myUserId = me.Id;

        // making sure the game is actually there, we'll run into problems otherwise
        var gameIsInDb = false; 
        gameIsInDb = _gameRepository.GetGameByAppId( appId ) != null;
        if( !gameIsInDb )
        {
            var gamesToAdd = new List<Game>();
            gamesToAdd = (await _steamService.GetGamesAsync( mySteamId, myUserId )).ToList<Game>();
            foreach( var game in gamesToAdd )
            {
                _gameRepository.AddOrUpdate( game );
            }
        }

        var foundGame = _gameRepository.GetGameByAppId(appId);
        if ( foundGame is null )
            return RedirectToAction( nameof( Index ) );
        var gameIdFound = foundGame.Id;

        var gameAchievements = new List<GameAchievement>();
        gameAchievements = _gameAchievementRepository.GetAchievementsFromGameId( gameIdFound );
        if( gameAchievements.Count() == 0 )
        {
            var schema = await _steamService.GetSchemaAsync( appId );
            var rawAchievements = schema?.game?.availableGameStats?.achievements?.ToList<SchemaAchievement>() ?? new List<SchemaAchievement>();

            foreach( var rawAch in rawAchievements )
            {
                var addMe = new GameAchievement( rawAch );
                addMe.GameId = gameIdFound;
                gameAchievements.Add(addMe);
                _gameAchievementRepository.AddOrUpdate(addMe);
            }
        }
        
        var myAchievements = new List<UserAchievement>();
        myAchievements = _userAchievementRepository.GetAchievementsByGameAndUserId( gameIdFound, myUserId ).Where( a => a.Achieved == false ).ToList<UserAchievement>();
        if( myAchievements.Count() == 0 )
        {
            var myRawAchievementData = await _steamService.GetAchievementsAsync( mySteamId, appId );
            var myAchievementList = myRawAchievementData?.playerstats?.achievements ?? new List<Achievement>();

            foreach( var gAch in gameAchievements )
            {
                foreach( var myAch in myAchievementList )
                {
                    if( myAch.apiname == gAch.ApiName )
                    {
                        var myUserAch = new UserAchievement( gAch, myAch );
                        myUserAch.OwnerId = myUserId;
                        if( !myUserAch.Achieved )
                            myAchievements.Add( myUserAch );
                        _userAchievementRepository.AddOrUpdate( myUserAch );
                    }
                }
            }
        }

        var theirRawAchievementData = await _steamService.GetAchievementsAsync( friendSteamId, appId );
        var theirAchievementList = theirRawAchievementData?.playerstats?.achievements ?? new List<Achievement>();

        var theirAchievements = new List<UserAchievement>();
        foreach( var gAch in gameAchievements )
        {
            foreach( var theirAch in theirAchievementList )
            {
                if( theirAch.apiname == gAch.ApiName )
                {
                    var theirUserAch = new UserAchievement( gAch, theirAch );
                    if( !theirUserAch.Achieved )
                        theirAchievements.Add( theirUserAch );
                }
            }
        }

        var ( myShared, theirShared ) = UserAchievement.ComputeSharedMissing( myAchievements, theirAchievements );
        myAchievements = myShared;
        theirAchievements = theirShared;

        var viewModel = new CompeteInitiateVM( myAchievements, theirAchievements );

        var gameInUse = new Game();
        gameInUse = _gameRepository.GetGameById( gameIdFound );

        viewModel.ChosenGame = gameInUse;

        var compsWithUser = new List<CompetitionPlayer>();
        compsWithUser = _competitionPlayerRepository.GetCompetitionIdsBySteamId( mySteamId );

        var existingCompetition = new Competition();
        if( compsWithUser != null && compsWithUser.Count() != 0 )
        {
            foreach( var compPlayer in compsWithUser )
            {
                existingCompetition = _competitionRepository.GetCompetitionByCompPlayerAndGameId( compPlayer, gameIdFound );
                
                if( existingCompetition != null )
                    break;
            }
        }
        else {
            existingCompetition = null;
        }
        
        //refactor to save these values to the viewmodel to make it easier to pass around
        viewModel.MySteamId = mySteamId;
        viewModel.MyFriendId = friendSteamId;
        // When there's no existing competition for this friend+game, hand the view a fresh one (with
        // default dates + an empty players list) so it renders the "create" form instead of NRE'ing on
        // Model.CurrentCompetition.* (was a 500 for the common "no existing competition" case).
        viewModel.CurrentCompetition = existingCompetition ?? new Competition
        {
            GameId = gameIdFound,
            StartDate = DateTime.Now,
            EndDate = DateTime.Now.AddDays( 7 ),
            CompetitionPlayers = new List<CompetitionPlayer>()
        };
        


        return View( viewModel );
    }

    // The Initiate POST action is RETIRED. It used to bind the whole CompeteInitiateVM from the
    // form and hand competeIn.CurrentCompetition straight to AddOrUpdate, so a request-supplied
    // Competition.Id overwrote an arbitrary existing row (its dates, CreatorId, StatusId) and the
    // player/achievement writes reused that same Id. Nothing may write a Competition whose Id came
    // from the request. Views/Compete/Initiate.cshtml now posts the opponent, game, dates and
    // shared-missing achievements straight into the validated Create flow below, which builds the
    // Competition server-side with a fresh, DB-assigned Id.

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create( CompeteCreateVM compCreatedOut )
    {
        var me = CurrentDomainUser();
        if ( me is null ) return RedirectToAction( nameof( Index ) );
        var CurrentUserId = me.Id;

        var game = _gameRepository.GetGameByAppId( compCreatedOut.GameAppId );
        if ( game is null ) return RedirectToAction( nameof( Index ) );

        var comp = new Competition()
        {
            CreatorId = CurrentUserId,
            GameId = game.Id,
            StartDate = compCreatedOut.CompStartTime,
            EndDate = compCreatedOut.CompEndTime,
            Game = game,
        };

        comp.Status = _statusRepository.GetStatusByName("Active");

        _competitionRepository.AddOrUpdate( comp );

        _inboxService.SendMessage(CurrentUserId, SteamProject.Helpers.SystemUsers.SystemSenderId, $"You started a new achievement competition for {comp.Game.Name}! Starting on {comp.StartDate.ToLocalTime()} and finishing {comp.EndDate.ToLocalTime()}");

        var competitors = new List<CompetitionPlayer>();
        competitors.Add( new CompetitionPlayer() { CompetitionId = comp.Id, SteamId = compCreatedOut.SteamId } );
        
        if( compCreatedOut.OpponentId != null )
            competitors.Add( new CompetitionPlayer() { CompetitionId = comp.Id, SteamId = compCreatedOut.OpponentId } );
        
        if( compCreatedOut.OpponentIds != null )
            foreach( string id in compCreatedOut.OpponentIds )
            {
                competitors.Add( new CompetitionPlayer() { CompetitionId = comp.Id, SteamId = id, Competition = comp } );
            }

        foreach( CompetitionPlayer opp in competitors )
        {
            _competitionPlayerRepository.AddOrUpdate( opp );
        }

        var presentAchievements = new List<GameAchievement>();
        presentAchievements = _gameAchievementRepository.GetAchievementsFromGameId( game.Id );        

        if( presentAchievements.Count() <= 0 )
        {
            var schemaResult = await _steamService.GetSchemaAsync( game.AppId );
            var achievementList = schemaResult?.game?.availableGameStats?.achievements ?? new List<SchemaAchievement>();

            foreach( SchemaAchievement ach in achievementList )
            {   
                var newAch = new GameAchievement( ach );
                _gameAchievementRepository.AddOrUpdate( newAch );
                presentAchievements.Add( newAch );
            }
        }

        var achievementsCompeting = new List<GameAchievement>();
        foreach( var achUnearned in compCreatedOut.AchievementDisplayNames )
        {
            foreach( var gameAch in presentAchievements )
            {
                if( achUnearned == gameAch.DisplayName )
                    achievementsCompeting.Add( gameAch );
            }
        }

        foreach( GameAchievement ach in achievementsCompeting )
        {
            var compAch = new CompetitionGameAchievement() { CompetitionId = comp.Id, GameAchievementId = ach.Id };
            _competitionGameAchievementRepository.AddOrUpdate( compAch );
        }

        return RedirectToAction("Index");
    }

    
}
