using Microsoft.EntityFrameworkCore;
using Moq;
using SteamProject.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using System.Diagnostics;
using System.Net;
using SteamProject.DAL.Abstract;
using SteamProject.DAL.Concrete;
using System;

namespace NUnit_Tests.RepoTesting
{
    public class UserRepositoryTests
    {
        private Mock<SteamInfoDbContext> _mockContext;
        private Mock<DbSet<User>> _mockPersonDbSet;

        private List<User> _users;
        private List<Friend> _friends;
        private List<UserGameInfo> _userGameInfo;
        private List<Game> _game;

        private User MakeValidPerson()
        {
            User newPerson = new User
            {
                Id = 1,
                SteamId = "76561190000000101",
                SteamName = "TestUser1",
                Friends = new List<Friend>(),
                UserAchievements = new List<UserAchievement>(),
                UserGameInfos = new List<UserGameInfo>(),
                AspNetUserId = "f7b530ec-bf89-45e1-8080-cbe3bfd0f08a"
            };
            return newPerson;
        }

        [SetUp]
        public void Setup()
        {
            _users = new List<User>
            {
                new User {Id= 1, SteamName = "TestUser1", SteamId = "76561190000000103", AspNetUserId = "f7b530ec-bf89-45e1-8080-cbe3bfd0f08a" },
                new User {Id= 2, SteamName = "TestUser2", SteamId = "76561190000000104", AspNetUserId = "f7b530ec-bf89-45e1-8080-cbe3bfd0f08g"},
                new User {Id= 3, SteamName = "TestUser3", SteamId = "76561190000000102", AspNetUserId = "f7b530ec-bf89-45e1-8080-cbe3bfd0f08h"}

            };
            _friends = new List<Friend>
            {
                new Friend {Id = 1, RootId = 1, SteamId = "76561190000000104"},
                new Friend {Id = 2, RootId = 2, SteamId = "76561190000000103"},
                new Friend {Id = 3, RootId = 2, SteamId = "76561190000000102"},
            };
            

            _userGameInfo = new List<UserGameInfo>()
            {
                new UserGameInfo { Id = 1, Followed = true, GameId = 1, Hidden = false, OwnerId = 1},
                new UserGameInfo { Id = 2, Followed = true, GameId = 2, Hidden = false, OwnerId = 1},
                new UserGameInfo { Id = 3, Followed = true, GameId = 2, Hidden = true, OwnerId = 1},
            };
            _game = new List<Game>()
            {
                new Game {Id = 1,AppId = 310560, Name = "DiRT Rally"},
                new Game {Id = 2,AppId = 218620, Name = "PAYDAY 2"},
                new Game {Id = 2,AppId = 632360, Name = "Risk of Rain 2"},
            };


            _users.ForEach(p =>
            {
                p.Friends = _friends.Where(i => i.Id == p.Id).ToList();
                p.UserGameInfos = _userGameInfo.Where(i => i.Id == p.Id).ToList();
            });


            // Finally, mock the context and dbsets
            _mockContext = new Mock<SteamInfoDbContext>();
            _mockPersonDbSet = MockHelpers.GetMockDbSet(_users.AsQueryable());
            _mockContext.Setup(ctx => ctx.Users).Returns(_mockPersonDbSet.Object);
            _mockContext.Setup(ctx => ctx.Set<User>()).Returns(_mockPersonDbSet.Object);
        }

        [Test]
        public void GetUserById_WithThreeItems_ReturnsTrue()
        {
            // Arrange
            IUserRepository userRepository = new UserRepository(_mockContext.Object);
            User expected = MakeValidPerson();


            // Act
            User actual = userRepository.GetUser("f7b530ec-bf89-45e1-8080-cbe3bfd0f08a");

            // Assert
            Assert.Multiple(() =>
            {
                Assert.True(actual.SteamName == expected.SteamName);
            });
        }

        [Test]
        public void GetUserById_WithWrongUser_ReturnsFalse()
        {
            // Arrange
            IUserRepository userRepository = new UserRepository(_mockContext.Object);
            User expected = MakeValidPerson();


            // Act
            User actual = userRepository.GetUser("f7b530ec-bf89-45e1-8080-cbe3bfd0f08g");

            // Assert
            Assert.Multiple(() =>
            {
                Assert.False(actual.SteamName == expected.SteamName);
            });
        }
        [Test]
        public void GetUserById_WithNoItems_ReturnsTrue_ThrowsNull()
        {
            // Arrange

            _users.Clear();
            _mockContext = new Mock<SteamInfoDbContext>();
            _mockPersonDbSet = MockHelpers.GetMockDbSet(_users.AsQueryable());
            _mockContext.Setup(ctx => ctx.Users).Returns(_mockPersonDbSet.Object);
            _mockContext.Setup(ctx => ctx.Set<User>()).Returns(_mockPersonDbSet.Object);
            IUserRepository userRepository = new UserRepository(_mockContext.Object);

            // Act
            

            // Assert
            Assert.IsNull(userRepository.GetUser("f7b530ec-bf89-45e1-8080-cbe3bfd0f08a"));
        }

        [Test]
        public void GetUsersBySteamId_ReturnsOnlyRequestedIds_FromTwelveUsers()
        {
            // Arrange — 12 users in the table; request only 3 by SteamId.
            // This proves the filter is pushed down, not a whole-table load.
            var allUsers = Enumerable.Range(1, 12)
                .Select(i => new User
                {
                    Id = i,
                    SteamId = $"76561198{i:D8}",
                    SteamName = $"User{i}"
                })
                .ToList();

            var mockContext = new Mock<SteamInfoDbContext>();
            var mockSet = MockHelpers.GetMockDbSet(allUsers.AsQueryable());
            mockContext.Setup(ctx => ctx.Users).Returns(mockSet.Object);
            mockContext.Setup(ctx => ctx.Set<User>()).Returns(mockSet.Object);

            var repo = new UserRepository(mockContext.Object);

            // Pick 3 distinct SteamIds from the 12
            var requestedIds = new[] { allUsers[2].SteamId!, allUsers[5].SteamId!, allUsers[9].SteamId! };
            var expectedIds = requestedIds
                .ToDictionary(id => id, id => allUsers.First(u => u.SteamId == id).Id);

            // Act
            var result = repo.GetUsersBySteamId(requestedIds);

            // Assert — exactly 3 rows, all requested, correct Ids, no leaks
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(3), "Must return exactly the requested count");

            foreach (var (steamId, id) in expectedIds)
            {
                var row = result.FirstOrDefault(s => s.SteamId == steamId);
                Assert.That(row, Is.Not.Null, $"Expected SteamId {steamId} in result");
                Assert.That(row!.Id, Is.EqualTo(id), $"Id for SteamId {steamId} must match");
            }

            // No row outside the request set may appear: returned SteamIds == requested SteamIds.
            Assert.That(result.Select(s => s.SteamId).OrderBy(x => x),
                Is.EqualTo(requestedIds.OrderBy(x => x)),
                "Every returned SteamId must be a requested id (and vice versa)");
        }

        [Test]
        public void GetUsersBySteamId_WithUnmatchedIds_ReturnsEmpty()
        {
            // Arrange — none of the requested SteamIds exist in the table
            var allUsers = Enumerable.Range(1, 10)
                .Select(i => new User
                {
                    Id = i,
                    SteamId = $"76561198{i:D8}",
                    SteamName = $"User{i}"
                })
                .ToList();

            var mockContext = new Mock<SteamInfoDbContext>();
            var mockSet = MockHelpers.GetMockDbSet(allUsers.AsQueryable());
            mockContext.Setup(ctx => ctx.Users).Returns(mockSet.Object);
            mockContext.Setup(ctx => ctx.Set<User>()).Returns(mockSet.Object);

            var repo = new UserRepository(mockContext.Object);

            var nonExistent = new[] { "00000000000000001", "00000000000000002" };

            // Act
            var result = repo.GetUsersBySteamId(nonExistent);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Empty, "No matching users should be returned");
        }

    }
}
