using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Headers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SteamProject.DAL.Abstract;
using SteamProject.Helpers;
using SteamProject.Models;
using SteamProject.Models.Awards.Abstract;
using SteamProject.Models.DTO;
using SteamProject.Services;
using SteamProject.ViewModels;

namespace SteamProject.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class BadgeController : ControllerBase
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IBadgeRepository _badgeRepository;
        private readonly IUserBadgeRepository _userBadgeRepository;
        private readonly IEnumerable<IAwardCondition> _awardConditions;
        private readonly IUserRepository _userRepository;
        private readonly IInboxService _inboxService;

        public BadgeController(UserManager<IdentityUser> userManager, IBadgeRepository badgeRepository, IUserBadgeRepository userBadgeRepository, IEnumerable<IAwardCondition> awardConditions, IUserRepository userRepository, IInboxService inboxService)
        {
            _userManager = userManager;
            _badgeRepository = badgeRepository;
            _userBadgeRepository = userBadgeRepository;
            _awardConditions = awardConditions;
            _userRepository = userRepository;
            _inboxService = inboxService;
        }

        [HttpGet("CheckForNewBadges")]
        public async Task<ActionResult<List<int>>> CheckForNewBadges()
        {
            string? id = _userManager.GetUserId(User);

            if (id is null)
            {
                return BadRequest(new { success = false, message = "User not found" });
            }
            else
            {
                // Eager-load the collections the award conditions read (avoids a per-poll N+1 storm).
                User user = await _userRepository.GetUserWithAwardDataAsync(id);

                if (user == null)
                {
                    return BadRequest(new { success = false, message = "User not found" });
                }

                List<int> awardedBadges = new List<int>();

                foreach (var awardCondition in _awardConditions)
                {
                    // Check if the condition is fulfilled
                    bool isFulfilled = await awardCondition.IsFulfilledAsync(user, _userBadgeRepository);

                    if (isFulfilled)
                    {
                        // Find the badge with the same name as the award condition
                        var badge = _badgeRepository.GetAll().FirstOrDefault(b => b.Name == awardCondition.BadgeName);

                        // Check if the badge is null, and continue to the next iteration if it is
                        if (badge == null)
                        {
                            continue;
                        }

                        // Award the badge to the user. Insert FIRST: the unique (UserId, BadgeId) index is
                        // the source of truth against a concurrent-poll race where two requests both pass
                        // the "already earned?" check above. Only message + report on a successful insert,
                        // so a losing race produces no duplicate row and no spam notification.
                        UserBadge userBadge = new UserBadge
                        {
                            UserId = user.Id,
                            BadgeId = badge.Id,
                        };

                        try
                        {
                            await _userBadgeRepository.AddOrUpdateAsync(userBadge);
                        }
                        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
                        {
                            continue; // already awarded by a concurrent request
                        }

                        await _inboxService.SendMessageAsync(user.Id, SteamProject.Helpers.SystemUsers.SystemSenderId, $"Looks like you earned the {badge.Name} badge, keep it up and try to collect them all!");
                        awardedBadges.Add(badge.Id);
                    }
                }

                return Ok(awardedBadges);
            }
        }



        [HttpGet("GetBadgeDetails/{badgeId}")]
        public ActionResult<BadgeData> GetBadgeDetails(int badgeId)
        {
            var badge = _badgeRepository.FindById(badgeId);
            if (badge == null) return NotFound(new { success = false, message = "Badge not found" });

            if (badge.Image == null || badge.Image.Length == 0)
                return NotFound(new { success = false, message = "Badge image not available" });

            var badgeData = new BadgeData
            {
                Id = badge.Id,
                Name = badge.Name,
                Description = badge.Description,
                ImageFileName = $"{badge.Id}.png", // Update this with your preferred image file name format.
                ImageData = badge.Image
            };

            return Ok(badgeData);
        }


        [HttpGet("GetBadgeImage/{badgeId}")]
        public IActionResult GetBadgeImage(int badgeId)
        {
            var badge = _badgeRepository.FindById(badgeId);
            if (badge == null) return NotFound(new { success = false, message = "Badge not found" });

            if (badge.Image == null || badge.Image.Length == 0)
                return NotFound(new { success = false, message = "Badge image not available" });

            // SHA256 always yields 64 hex chars, so the 16-char slice below is safe for any image size.
            string hex = Convert.ToHexString(SHA256.HashData(badge.Image)).ToLowerInvariant();
            string etagValue = $"\"{hex.Substring(0, 16)}\"";

            if (Request.Headers.IfNoneMatch.Any(h => h == etagValue))
                return StatusCode(304);

            Response.Headers["Cache-Control"] = "public, max-age=31536000";
            Response.Headers["ETag"] = etagValue;

            return File(badge.Image, "image/png");
        }
    }
}
