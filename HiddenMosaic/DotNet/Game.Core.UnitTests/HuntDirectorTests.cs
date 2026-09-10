using Game.Core;
using NUnit.Framework;

namespace Game.Core.Tests
{
    [TestFixture]
    public class HuntDirectorTests
    {
        [Test]
        public void WavesThenLevelComplete()
        {
            var catalog = new HuntCatalog(new[]
            {
                new HuntLevelDef("horse.json", "Horse", waveSize: 2, waveCount: 2)
            });
            var director = new HuntDirector(catalog);
            director.BeginLevel(0, new[] { "cup", "key", "leaf", "moon" });
            Assert.That(director.Session.Remaining, Is.EqualTo(new[] { "cup", "key" }));
            Assert.That(director.TryFind("cup"), Is.True);
            Assert.That(director.IsLevelComplete, Is.False);
            Assert.That(director.TryFind("key"), Is.True);
            Assert.That(director.WaveIndex, Is.EqualTo(1));
            Assert.That(director.Session.Remaining, Is.EqualTo(new[] { "leaf", "moon" }));
            Assert.That(director.TryFind("leaf"), Is.True);
            Assert.That(director.TryFind("moon"), Is.True);
            Assert.That(director.IsLevelComplete, Is.True);
            Assert.That(director.IsCampaignComplete, Is.True);
        }

        [Test]
        public void NextLevelAdvancesCatalog()
        {
            var catalog = new HuntCatalog(new[]
            {
                new HuntLevelDef("a.json", "A", waveSize: 1, waveCount: 1),
                new HuntLevelDef("b.json", "B", waveSize: 1, waveCount: 1)
            });
            var director = new HuntDirector(catalog);
            director.BeginLevel(0, new[] { "star" });
            Assert.That(director.TryFind("star"), Is.True);
            Assert.That(director.HasNextLevel, Is.True);
            Assert.That(director.TryNextLevel(new[] { "moon" }), Is.True);
            Assert.That(director.Current.Title, Is.EqualTo("B"));
            Assert.That(director.IsLevelComplete, Is.False);
            Assert.That(director.TryFind("moon"), Is.True);
            Assert.That(director.IsCampaignComplete, Is.True);
        }
    }
}
