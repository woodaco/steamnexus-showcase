using NUnit.Framework;
using SteamProject.Models;

namespace NUnit_Tests.ModelTesting
{
    public class ComputeSharedMissingTests
    {
        private static UserAchievement MakeAch( int id, bool achieved )
        {
            return new UserAchievement
            {
                AchievementId = id,
                Achieved = achieved,
            };
        }

        [Test]
        public void EmptyInputs_ReturnsEmptyLists()
        {
            var ( mine, theirs ) = UserAchievement.ComputeSharedMissing(
                new List<UserAchievement>(), new List<UserAchievement>() );

            Assert.That( mine, Is.Empty );
            Assert.That( theirs, Is.Empty );
        }

        [Test]
        public void NoOverlap_ReturnsEmptyLists()
        {
            var my = new List<UserAchievement> { MakeAch( 1, false ), MakeAch( 2, false ) };
            var their = new List<UserAchievement> { MakeAch( 3, false ), MakeAch( 4, false ) };

            var ( mine, theirs ) = UserAchievement.ComputeSharedMissing( my, their );

            Assert.That( mine, Is.Empty );
            Assert.That( theirs, Is.Empty );
        }

        [Test]
        public void PartialOverlap_ReturnsOnlySharedIds()
        {
            var my = new List<UserAchievement> { MakeAch( 1, false ), MakeAch( 2, false ), MakeAch( 3, false ) };
            var their = new List<UserAchievement> { MakeAch( 2, false ), MakeAch( 3, false ), MakeAch( 4, false ) };

            var ( mine, theirs ) = UserAchievement.ComputeSharedMissing( my, their );

            // Shared ids: 2 and 3
            Assert.That( mine, Has.Count.EqualTo( 2 ) );
            Assert.That( mine.Select( a => a.AchievementId ).OrderBy( x => x ), Is.EquivalentTo( new[] { 2, 3 } ) );
            Assert.That( theirs, Has.Count.EqualTo( 2 ) );
            Assert.That( theirs.Select( a => a.AchievementId ).OrderBy( x => x ), Is.EquivalentTo( new[] { 2, 3 } ) );
        }

        [Test]
        public void DuplicateAchievementIds_AreDeDuplicated()
        {
            var my = new List<UserAchievement> { MakeAch( 1, false ), MakeAch( 1, false ), MakeAch( 2, false ) };
            var their = new List<UserAchievement> { MakeAch( 1, false ), MakeAch( 1, false ), MakeAch( 2, false ) };

            var ( mine, theirs ) = UserAchievement.ComputeSharedMissing( my, their );

            // Each id should appear exactly once
            Assert.That( mine, Has.Count.EqualTo( 2 ) );
            Assert.That( mine.Select( a => a.AchievementId ).Distinct(), Is.EquivalentTo( new[] { 1, 2 } ) );
            Assert.That( theirs, Has.Count.EqualTo( 2 ) );
            Assert.That( theirs.Select( a => a.AchievementId ).Distinct(), Is.EquivalentTo( new[] { 1, 2 } ) );
        }

        [Test]
        public void ReturnedLists_AreNotReferenceEqual()
        {
            var my = new List<UserAchievement> { MakeAch( 1, false ) };
            var their = new List<UserAchievement> { MakeAch( 1, false ) };

            var ( mine, theirs ) = UserAchievement.ComputeSharedMissing( my, their );

            Assert.That( ReferenceEquals( mine, theirs ), Is.False,
                "The two returned lists must be independent instances" );
        }

        [Test]
        public void AchievedEntries_AreFilteredOut()
        {
            // Both players have achievement 1 achieved — it should NOT appear in the result
            var my = new List<UserAchievement> { MakeAch( 1, true ), MakeAch( 2, false ) };
            var their = new List<UserAchievement> { MakeAch( 1, true ), MakeAch( 2, false ) };

            var ( mine, theirs ) = UserAchievement.ComputeSharedMissing( my, their );

            // Only id 2 is missing on both sides
            Assert.That( mine, Has.Count.EqualTo( 1 ) );
            Assert.That( mine[0].AchievementId, Is.EqualTo( 2 ) );
        }

        [Test]
        public void OneSideHasId_AchievedOtherSide_Missing_NotShared()
        {
            // Player A has id 1 achieved, Player B has id 1 missing — not both missing
            var my = new List<UserAchievement> { MakeAch( 1, true ), MakeAch( 2, false ) };
            var their = new List<UserAchievement> { MakeAch( 1, false ), MakeAch( 2, false ) };

            var ( mine, theirs ) = UserAchievement.ComputeSharedMissing( my, their );

            // Only id 2 is missing on both sides
            Assert.That( mine, Has.Count.EqualTo( 1 ) );
            Assert.That( mine[0].AchievementId, Is.EqualTo( 2 ) );
        }
    }
}
