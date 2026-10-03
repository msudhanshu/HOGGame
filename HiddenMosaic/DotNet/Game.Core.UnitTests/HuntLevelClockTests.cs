using Game.Core;
using NUnit.Framework;

namespace Game.Core.Tests
{
    [TestFixture]
    public class HuntLevelClockTests
    {
        [Test]
        public void TickCountsDownAndExpires()
        {
            var clock = new HuntLevelClock();
            clock.Reset(10f);
            Assert.That(clock.IsExpired, Is.False);
            clock.Tick(4f);
            Assert.That(clock.Remaining, Is.EqualTo(6f).Within(0.001f));
            clock.Tick(6f);
            Assert.That(clock.IsExpired, Is.True);
            Assert.That(clock.Remaining, Is.EqualTo(0f));
        }

        [Test]
        public void StopPreventsExpiry()
        {
            var clock = new HuntLevelClock();
            clock.Reset(2f);
            clock.Stop();
            clock.Tick(5f);
            Assert.That(clock.Remaining, Is.EqualTo(2f));
            Assert.That(clock.IsExpired, Is.False);
        }
    }
}
