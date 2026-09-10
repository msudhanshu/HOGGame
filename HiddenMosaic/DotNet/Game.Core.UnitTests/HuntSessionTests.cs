using Game.Core;
using NUnit.Framework;

namespace Game.Core.Tests
{
    [TestFixture]
    public class HuntSessionTests
    {
        [Test]
        public void TryFindRemovesTarget()
        {
            var session = new HuntSession(new[] { "circle", "star" });
            Assert.That(session.TryFind("circle"), Is.True);
            Assert.That(session.Remaining, Is.EqualTo(new[] { "star" }));
            Assert.That(session.Found, Is.EqualTo(new[] { "circle" }));
            Assert.That(session.IsComplete, Is.False);
        }

        [Test]
        public void WrongNameDoesNotCount()
        {
            var session = new HuntSession(new[] { "blob" });
            Assert.That(session.TryFind("circle"), Is.False);
            Assert.That(session.IsComplete, Is.False);
        }

        [Test]
        public void CompletesWhenLastTargetFound()
        {
            var session = new HuntSession(new[] { "triangle" });
            Assert.That(session.TryFind("triangle"), Is.True);
            Assert.That(session.IsComplete, Is.True);
            Assert.That(session.TryFind("triangle"), Is.False);
        }

        [Test]
        public void DuplicateTargetsNeedSeparateFinds()
        {
            var session = new HuntSession(new[] { "circle", "circle" });
            Assert.That(session.TryFind("circle"), Is.True);
            Assert.That(session.IsComplete, Is.False);
            Assert.That(session.TryFind("circle"), Is.True);
            Assert.That(session.IsComplete, Is.True);
        }
    }
}
