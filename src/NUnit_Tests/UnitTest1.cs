using NUnit.Framework;
using SteamProject.Models;

namespace NUnit_Tests
{
    /// <summary>
    /// GapProcessor maps an achievement's global-unlock rarity (%) to a point value:
    /// the rarer the achievement (lower %), the more points it is worth.
    /// </summary>
    public class GapProcessorTests
    {
        private GapProcessor _gap = null!;

        [SetUp]
        public void Setup() => _gap = new GapProcessor();

        [TestCase(95.0, 1)]   // very common -> fewest points
        [TestCase(90.0, 1)]   // boundary
        [TestCase(80.0, 3)]
        [TestCase(75.0, 3)]   // boundary
        [TestCase(65.0, 5)]
        [TestCase(60.0, 5)]   // boundary
        [TestCase(40.0, 7)]
        [TestCase(35.0, 7)]   // boundary
        [TestCase(10.0, 9)]
        [TestCase(1.1, 9)]    // boundary
        [TestCase(0.5, 11)]   // extremely rare -> most points
        public void HandlePercent_maps_rarity_to_points(double percent, int expected)
        {
            Assert.That(_gap.HandlePercent(percent), Is.EqualTo(expected));
        }
    }
}
