using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SteamProject.DAL.Abstract;
using SteamProject.Models;
using SteamProject.Models.DTO;

namespace SteamProject.Controllers
{
    [Authorize]
    [AutoValidateAntiforgeryToken]
    [Route("api/[controller]")]
    [ApiController]
    public class VoteController : ControllerBase
    {
        private readonly ICompetitionVoteRepository _competitionVoteRepository;
        private readonly ICompetitionPlayerRepository _competitionPlayerRepository;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IUserRepository _userRepository;

        // Depend on the registered interfaces (the concrete types were never registered, so the
        // controller could not be constructed by DI at all).
        public VoteController(
            ICompetitionVoteRepository competitionVoteRepository,
            ICompetitionPlayerRepository competitionPlayerRepository,
            UserManager<IdentityUser> userManager,
            IUserRepository userRepository)
        {
            _competitionVoteRepository = competitionVoteRepository;
            _competitionPlayerRepository = competitionPlayerRepository;
            _userManager = userManager;
            _userRepository = userRepository;
        }

        /// <summary>Resolves the current authenticated user's domain <see cref="User"/> row, or null.</summary>
        private User? GetCurrentUser()
        {
            var identityId = _userManager.GetUserId(User);
            return identityId is null ? null : _userRepository.GetUser(identityId);
        }

        [HttpPost("competitionvote")]
        public async Task<ActionResult> CreateCompetitionVote([FromBody] CompetitionVoteRequest req)
        {
            var user = GetCurrentUser();
            if (user is null) return Unauthorized();

            // Only a participant of the competition may vote on its re-match (and it keeps the tally
            // bounded by the player count). Participation is by SteamId on CompetitionPlayer.
            var participants = _competitionPlayerRepository.GetAllForCompetition(req.CompetitionId);
            if (user.SteamId is null || !participants.Any(p => p.SteamId == user.SteamId))
                return StatusCode(403);

            // One vote per user per competition: update the existing vote instead of stacking duplicates
            // (otherwise a user could spam play-again votes and skew the majority tally).
            var existing = _competitionVoteRepository.GetAll()
                .FirstOrDefault(v => v.CompetitionId == req.CompetitionId && v.UserId == user.Id);
            if (existing is not null)
            {
                existing.WantsToPlayAgain = req.WantsToPlayAgain;
                await _competitionVoteRepository.UpdateVoteAsync(existing);
                return Ok();
            }

            // Map from a request DTO (binding the EF entity made its non-nullable nav properties
            // "required" -> 400, and allowed mass-assignment). The voter is always the auth user.
            var vote = new CompetitionVote
            {
                CompetitionId = req.CompetitionId,
                WantsToPlayAgain = req.WantsToPlayAgain,
                UserId = user.Id
            };
            await _competitionVoteRepository.AddVoteAsync(vote);
            return Ok();
        }

        [HttpPut("competitionvote/{id}")]
        public async Task<ActionResult> UpdateCompetitionVote(int id, [FromBody] CompetitionVoteRequest req)
        {
            var user = GetCurrentUser();
            if (user is null) return Unauthorized();
            if (id != req.Id) return BadRequest();

            // Look up the existing vote by PK; 404 when absent.
            var vote = _competitionVoteRepository.FindById(id);
            if (vote is null) return NotFound();

            // Ownership check: the caller must own this vote row.
            if (vote.UserId != user.Id) return StatusCode(403);

            // Participant check: only a competition participant may update the vote.
            var participants = _competitionPlayerRepository.GetAllForCompetition(req.CompetitionId);
            if (user.SteamId is null || !participants.Any(p => p.SteamId == user.SteamId))
                return StatusCode(403);

            // Mutate the loaded entity.
            vote.WantsToPlayAgain = req.WantsToPlayAgain;
            await _competitionVoteRepository.UpdateVoteAsync(vote);
            return Ok();
        }
    }
}
