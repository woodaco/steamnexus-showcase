using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit_Tests.RepoTesting;
using SteamProject.DAL.Abstract;
using SteamProject.DAL.Concrete;
using SteamProject.Models;

namespace NUnit_Tests.RepoTesting.CompetitionTests;

public class CompetitionGameAchievementRepositoryTests
{
    private Mock<SteamInfoDbContext> _mockContext;
    private Mock<DbSet<CompetitionGameAchievement>> _mockCompetitionGameAchievementDbSet;

    private List<CompetitionGameAchievement> _competitionGameAchievements; 

    [SetUp]
    public void Setup()
    {
        _competitionGameAchievements = new List<CompetitionGameAchievement>
        {
            new CompetitionGameAchievement { CompetitionId = 1, GameAchievementId = 1 },
            new CompetitionGameAchievement { CompetitionId = 1, GameAchievementId = 2 },
            new CompetitionGameAchievement { CompetitionId = 1, GameAchievementId = 3 },
            new CompetitionGameAchievement { CompetitionId = 1, GameAchievementId = 4 },
            new CompetitionGameAchievement { CompetitionId = 1, GameAchievementId = 5 },

            new CompetitionGameAchievement { CompetitionId = 2, GameAchievementId = 1 },
            new CompetitionGameAchievement { CompetitionId = 2, GameAchievementId = 2 },
            new CompetitionGameAchievement { CompetitionId = 2, GameAchievementId = 3 },
            new CompetitionGameAchievement { CompetitionId = 2, GameAchievementId = 4 },
            new CompetitionGameAchievement { CompetitionId = 2, GameAchievementId = 5 },
        };

        _mockContext = new Mock<SteamInfoDbContext>();
        _mockCompetitionGameAchievementDbSet = MockHelpers.GetMockDbSet(_competitionGameAchievements.AsQueryable());
        _mockContext.Setup(ctx => ctx.CompetitionGameAchievements).Returns(_mockCompetitionGameAchievementDbSet.Object);
        _mockContext.Setup(ctx => ctx.Set<CompetitionGameAchievement>()).Returns(_mockCompetitionGameAchievementDbSet.Object);
    }

    [Test]
    public void GetByCompetitionId_IfNoMatch_ReturnsEmptyList()
    {
        _mockContext = new Mock<SteamInfoDbContext>();
        _mockCompetitionGameAchievementDbSet = MockHelpers.GetMockDbSet(_competitionGameAchievements.AsQueryable());
        _mockContext.Setup(ctx => ctx.Set<CompetitionGameAchievement>()).Returns(_mockCompetitionGameAchievementDbSet.Object);
        ICompetitionGameAchievementRepository compGameAchRepository = new CompetitionGameAchievementRepository(_mockContext.Object);

        var compGameAchList = compGameAchRepository.GetByCompetitionId( 0 );

        // Must be an empty list, never null: a competition with no tracked achievements previously
        // returned null here and NRE'd CompeteController.Details when it enumerated the result.
        Assert.That( compGameAchList, Is.Not.Null );
        Assert.That( compGameAchList, Is.Empty );
    }

    [Test]
    public void GetByCompetitionId_IfYesMatch_ReturnsList()
    {
        _mockContext = new Mock<SteamInfoDbContext>();
        _mockCompetitionGameAchievementDbSet = MockHelpers.GetMockDbSet(_competitionGameAchievements.AsQueryable());
        _mockContext.Setup(ctx => ctx.Set<CompetitionGameAchievement>()).Returns(_mockCompetitionGameAchievementDbSet.Object);
        ICompetitionGameAchievementRepository compGameAchRepository = new CompetitionGameAchievementRepository(_mockContext.Object);

        var compGameAchList = new List<CompetitionGameAchievement>();
        compGameAchList = compGameAchRepository.GetByCompetitionId( 1 );

        var compareList = new List<CompetitionGameAchievement>()
        {
            _competitionGameAchievements[0],
            _competitionGameAchievements[1],
            _competitionGameAchievements[2],
            _competitionGameAchievements[3],
            _competitionGameAchievements[4],
        };


        Assert.True( compGameAchList.SequenceEqual( compareList ) );
    }


}
