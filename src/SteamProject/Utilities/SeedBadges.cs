using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SteamProject.DAL.Abstract;
using SteamProject.Models;
using SteamProject.Models.DTO;

namespace SteamProject.Utilities
{
    public static class SeedBadges
    {
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            var badgeRepository = serviceProvider.GetRequiredService<IBadgeRepository>();
            var logger = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger("SeedBadges");

            var badgeDataJson = File.ReadAllText("Data/badges.json");
            var badgeData = JsonConvert.DeserializeObject<List<BadgeData>>(badgeDataJson) ?? new List<BadgeData>();

            // Idempotent + resilient: add any badge from the manifest that is not already present
            // (matched by Name), and skip+log a missing asset instead of aborting the whole seed.
            // EF assigns the Id; we never force it (the JSON has no id field).
            var existingNames = badgeRepository.GetAll().Select(b => b.Name).ToHashSet();
            var added = 0;

            foreach (var data in badgeData)
            {
                if (string.IsNullOrWhiteSpace(data.Name) || existingNames.Contains(data.Name))
                    continue;

                var imagePath = Path.Combine("wwwroot", "assets", "badges", data.ImageFileName);
                if (!File.Exists(imagePath))
                {
                    logger?.LogWarning("Skipping badge '{Name}': image not found at {Path}.", data.Name, imagePath);
                    continue;
                }

                badgeRepository.AddOrUpdate(new Badge
                {
                    Name = data.Name,
                    Description = data.Description,
                    Image = File.ReadAllBytes(imagePath)
                });
                existingNames.Add(data.Name);
                added++;
            }

            if (added > 0)
            {
                await badgeRepository.SaveChangesAsync();
                logger?.LogInformation("Seeded {Count} new badge(s).", added);
            }
        }
    }
}
