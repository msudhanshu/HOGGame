using Game.Core;
using NUnit.Framework;

namespace Game.Core.Tests
{
    [TestFixture]
    public class HuntLevelDefTests
    {
        [Test]
        public void ClampsTimeLimitAndToughness()
        {
            var def = new HuntLevelDef("a.json", "A", timeLimitSeconds: 10f, toughness: 9);
            Assert.That(def.TimeLimitSeconds, Is.EqualTo(30f));
            Assert.That(def.Toughness, Is.EqualTo(5));
        }

        [Test]
        public void TargetCountIsWaveProduct()
        {
            var def = new HuntLevelDef("a.json", "A", waveSize: 3, waveCount: 4);
            Assert.That(def.TargetCount, Is.EqualTo(12));
        }
    }
}
