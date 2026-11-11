using SteamProject.Models.DTO;
using SteamProject.Services;

namespace NUnit_Tests.ModelTesting
{
    /// <summary>
    /// Guards the GetManyUsers mapping bug: Steam's GetPlayerSummaries does not preserve request order
    /// and omits private/invalid steamids, so the response list is often shorter than the request. The
    /// old code consumed players[0] per requested id (RemoveAt(0)) which mismapped and threw once the
    /// list was exhausted. SteamService.MapManyUsers must key by steamid and stay total over the request.
    /// </summary>
    public class SteamMappingTests
    {
        private static SteamUserPOCO PocoWith(params (string id, string name)[] players)
        {
            return new SteamUserPOCO
            {
                response = new Response
                {
                    players = players
                        .Select(p => new Player { steamid = p.id, personaname = p.name })
                        .ToList()
                }
            };
        }

        [Test]
        public void MapManyUsers_OmittedAndReorderedPlayers_MapsEachRequestedIdInOrder_NoThrow()
        {
            // Steam returns only 2 of the 3 requested ids, and out of request order.
            var poco = PocoWith(("3", "Carol"), ("1", "Alice"));
            var requested = new List<string> { "1", "2", "3" };

            List<SteamProject.Models.User> users = null!;
            Assert.DoesNotThrow(() => users = SteamService.MapManyUsers(poco, requested));

            Assert.That(users.Count, Is.EqualTo(3), "one user per requested id, regardless of omissions");
            // Order matches the REQUEST, not the response.
            Assert.That(users[0].SteamId, Is.EqualTo("1"));
            Assert.That(users[0].SteamName, Is.EqualTo("Alice"));
            // The omitted (private/invalid) id still yields an identified placeholder so callers can align by SteamId.
            Assert.That(users[1].SteamId, Is.EqualTo("2"));
            Assert.That(users[1].SteamName, Is.Null);
            Assert.That(users[2].SteamId, Is.EqualTo("3"));
            Assert.That(users[2].SteamName, Is.EqualTo("Carol"));
        }

        [Test]
        public void MapManyUsers_NullResponse_ReturnsIdentifiedBlankUsers()
        {
            var requested = new List<string> { "10", "20" };

            var users = SteamService.MapManyUsers(null, requested);

            Assert.That(users.Count, Is.EqualTo(2));
            Assert.That(users.Select(u => u.SteamId), Is.EqualTo(new[] { "10", "20" }));
            Assert.That(users.All(u => u.SteamName is null), Is.True);
        }

        [Test]
        public void MapManyUsers_EmptyRequest_ReturnsEmpty()
        {
            Assert.That(SteamService.MapManyUsers(PocoWith(("1", "Alice")), new List<string>()), Is.Empty);
        }

        [Test]
        public void FillMissingProfilesFromLocal_FillsBlankNameAndAvatar_FromSameSteamId()
        {
            var fromSteam = SteamService.MapManyUsers(null, new List<string> { "100", "200" });
            var local = new List<SteamProject.Models.User>
            {
                new() { SteamId = "100", SteamName = "PixelRival", AvatarUrl = "/a.png" },
                new() { SteamId = "300", SteamName = "Unrelated" },
            };

            SteamService.FillMissingProfilesFromLocal(fromSteam, local);

            Assert.That(fromSteam[0].SteamName, Is.EqualTo("PixelRival"));
            Assert.That(fromSteam[0].AvatarUrl, Is.EqualTo("/a.png"));
            Assert.That(fromSteam[1].SteamName, Is.Null.Or.Empty);
        }

        [Test]
        public void FillMissingProfilesFromLocal_KeepsSteamData_WhenPresent()
        {
            var fromSteam = SteamService.MapManyUsers(PocoWith(("100", "LiveName")), new List<string> { "100" });
            var local = new List<SteamProject.Models.User> { new() { SteamId = "100", SteamName = "StaleName" } };

            SteamService.FillMissingProfilesFromLocal(fromSteam, local);

            Assert.That(fromSteam[0].SteamName, Is.EqualTo("LiveName"));
        }
    }
}
