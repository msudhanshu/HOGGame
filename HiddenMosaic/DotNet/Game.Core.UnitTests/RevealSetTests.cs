using Game.Core;
using NUnit.Framework;

namespace Game.Core.UnitTests
{
    public sealed class RevealSetTests
    {
        static RevealSetDef Set(params RevealObjectDef[] objects) => new RevealSetDef("park", 1000, 800, objects);

        [Test]
        public void ValidSetHasNoErrors()
        {
            var set = Set(new RevealObjectDef("lamp", 10, 10, 50, 80), new RevealObjectDef("cat", 400, 600, 90, 60));
            Assert.That(RevealSetValidator.Validate(set, 2), Is.Empty);
            Assert.That(set.ObjectNames(), Is.EqualTo(new[] { "lamp", "cat" }));
        }

        [Test]
        public void DuplicateNamesAreRejectedCaseInsensitive()
        {
            var set = Set(new RevealObjectDef("Lamp", 0, 0, 10, 10), new RevealObjectDef("lamp", 20, 0, 10, 10));
            Assert.That(RevealSetValidator.Validate(set), Has.Some.Contains("Duplicate"));
        }

        [Test]
        public void RectOutsideImageIsRejected()
        {
            var set = Set(new RevealObjectDef("ufo", 980, 10, 50, 50));
            Assert.That(RevealSetValidator.Validate(set), Has.Some.Contains("outside"));
        }

        [Test]
        public void TooFewObjectsForLevelIsRejected()
        {
            var set = Set(new RevealObjectDef("a", 0, 0, 10, 10));
            Assert.That(RevealSetValidator.Validate(set, 6), Has.Some.Contains("needs 6"));
        }

        static RevealPathDef Path(bool hole, params float[] pts) => new RevealPathDef(hole, pts);

        // Ring: outer square 100..200 with a 130..170 hole.
        static RevealPathDef[] Ring() => new[]
        {
            Path(false, 100, 100, 200, 100, 200, 200, 100, 200),
            Path(true, 130, 130, 170, 130, 170, 170, 130, 170)
        };

        [Test]
        public void OutlinedObjectIsValid()
        {
            var o = new RevealObjectDef("wheel", 100, 100, 100, 100, Ring(), "icons/wheel.png", 2,
                new[] { new RevealSeedDef(115, 150, true) }, 96, 96);
            Assert.That(RevealSetValidator.Validate(Set(o)), Is.Empty);
            Assert.That(o.HasOutline, Is.True);
            Assert.That(o.HasIconPlacement, Is.True);
        }

        [Test]
        public void BadPathsAreRejected()
        {
            var odd = new RevealObjectDef("a", 0, 0, 10, 10, new[] { Path(false, 0, 0, 5, 0, 5) });
            var tiny = new RevealObjectDef("b", 0, 0, 10, 10, new[] { Path(false, 0, 0, 5, 5) });
            var outside = new RevealObjectDef("c", 0, 0, 10, 10, new[] { Path(false, 0, 0, 5000, 0, 5, 5) });
            var holesOnly = new RevealObjectDef("d", 0, 0, 10, 10, new[] { Path(true, 0, 0, 5, 0, 5, 5) });
            var errors = RevealSetValidator.Validate(Set(odd, tiny, outside, holesOnly));
            Assert.That(errors, Has.Some.Contains("odd number"));
            Assert.That(errors, Has.Some.Contains("at least 3"));
            Assert.That(errors, Has.Some.Contains("point outside"));
            Assert.That(errors, Has.Some.Contains("only hole"));
        }

        [Test]
        public void ContainsRespectsOutlineAndHoles()
        {
            var ring = Ring();
            Assert.That(RevealGeometry.Contains(ring, 115, 150), Is.True); // on the band
            Assert.That(RevealGeometry.Contains(ring, 150, 150), Is.False); // in the hole
            Assert.That(RevealGeometry.Contains(ring, 90, 150), Is.False); // outside

            var tri = new[] { Path(false, 0, 0, 100, 0, 0, 100) };
            Assert.That(RevealGeometry.Contains(tri, 20, 20), Is.True);
            Assert.That(RevealGeometry.Contains(tri, 90, 90), Is.False); // inside the bounding box, outside the shape
        }

        [Test]
        public void AreaSubtractsHoles()
        {
            Assert.That(RevealGeometry.Area(Ring()), Is.EqualTo(100 * 100 - 40 * 40).Within(0.01f));
        }

        [Test]
        public void WithPathsRefitsRectAndTranslateMovesOutline()
        {
            var o = new RevealObjectDef("x", 0, 0, 1, 1).WithPaths(Ring());
            Assert.That((o.X, o.Y, o.Width, o.Height), Is.EqualTo((100, 100, 100, 100)));
            var moved = o.Translated(10, -5);
            Assert.That((moved.X, moved.Y), Is.EqualTo((110, 95)));
            Assert.That(moved.Paths[0].Points[0], Is.EqualTo(110f));
            Assert.That(moved.Paths[1].Points[1], Is.EqualTo(125f));
            Assert.That(o.WithRect(0, 0, 5, 5).HasOutline, Is.False);
            Assert.That(o.WithName("y").Paths.Count, Is.EqualTo(2));
        }

        [Test]
        public void LevelDefRevealModeDefaultsRevealSetToFileName()
        {
            var def = new HuntLevelDef("alien_city", "Aliens", mode: HuntLevelMode.Reveal);
            Assert.That(def.Mode, Is.EqualTo(HuntLevelMode.Reveal));
            Assert.That(def.RevealSet, Is.EqualTo("alien_city"));
            Assert.That(def.WithOverrides(4, 0, 1f).Mode, Is.EqualTo(HuntLevelMode.Reveal));
        }

        [Test]
        public void ParseModeIsLenient()
        {
            Assert.That(HuntLevelDef.ParseMode("Reveal"), Is.EqualTo(HuntLevelMode.Reveal));
            Assert.That(HuntLevelDef.ParseMode(""), Is.EqualTo(HuntLevelMode.Mosaic));
            Assert.That(HuntLevelDef.ParseMode(null), Is.EqualTo(HuntLevelMode.Mosaic));
        }
    }
}
