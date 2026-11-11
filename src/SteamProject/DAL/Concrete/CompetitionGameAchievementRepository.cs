using Microsoft.EntityFrameworkCore;
using SteamProject.DAL.Abstract;
using SteamProject.DAL.Concrete;
using SteamProject.Models;

namespace SteamProject.DAL.Concrete;

public class CompetitionGameAchievementRepository : Repository<CompetitionGameAchievement>,  ICompetitionGameAchievementRepository
{
    public CompetitionGameAchievementRepository(SteamInfoDbContext ctx) : base(ctx)
    {

    }

    public List<CompetitionGameAchievement> GetByCompetitionId( int id )
    {
        // Return an empty list (never null) so callers can enumerate safely — a competition with no
        // tracked achievements (e.g. a freshly-seeded one) previously NRE'd CompeteController.Details.
        return GetAll().Where( cga => cga.CompetitionId == id ).ToList();
    }
}