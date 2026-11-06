using System;
using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;

namespace Game.Core.Tests
{
    [TestFixture]
    public class DriftSwarmTests
    {
        const float FillerR = 0.16f;
        const float TargetR = 0.5f;
        const float Dt = 1f / 60f;

        /// <summary>Hex-packed disc of fillers, like a silhouette body.</summary>
        static List<DriftBody> Disc(float radius)
        {
            var bodies = new List<DriftBody>();
            var step = FillerR * 2f;
            var rowH = step * 0.866f;
            var row = 0;
            for (var y = -radius; y <= radius; y += rowH, row++)
            {
                var offset = (row & 1) == 0 ? 0f : step * 0.5f;
                for (var x = -radius + offset; x <= radius; x += step)
                {
                    if (x * x + y * y <= radius * radius)
                        bodies.Add(new DriftBody(x, y, FillerR));
                }
            }

            return bodies;
        }

        static float MaxHomeOffset(DriftSwarm swarm)
        {
            var max = 0f;
            for (var i = 0; i < swarm.FillerCount; i++)
                max = Math.Max(max, swarm.FillerHomeDistance(i));
            return max;
        }

        [Test]
        public void FillersRestAtHomeWithoutTargets()
        {
            var swarm = new DriftSwarm(Disc(3f), new List<DriftBody>(), new DriftSwarmTuning(), 1);
            for (var i = 0; i < 300; i++)
                swarm.Step(Dt);
            Assert.That(MaxHomeOffset(swarm), Is.LessThan(FillerR * 0.05f));
        }

        [Test]
        public void TargetPushesFillersAsideAndNeverOverlapsThem()
        {
            var fillers = Disc(4f);
            var swarm = new DriftSwarm(fillers, new List<DriftBody> { new DriftBody(0f, 0f, TargetR) }, new DriftSwarmTuning(), 2);
            for (var i = 0; i < 240; i++)
                swarm.Step(Dt);

            Assert.That(MaxHomeOffset(swarm), Is.GreaterThan(FillerR), "crowd should part around the target");
            swarm.GetTarget(0, out var tx, out var ty, out _, out _);
            for (var i = 0; i < swarm.FillerCount; i++)
            {
                swarm.GetFiller(i, out var fx, out var fy, out _, out _);
                var d = (float)Math.Sqrt((fx - tx) * (fx - tx) + (fy - ty) * (fy - ty));
                Assert.That(d, Is.GreaterThan(TargetR + FillerR * 0.6f), "filler " + i + " is buried under the target");
            }
        }

        [Test]
        public void TargetActuallyTravels()
        {
            var swarm = new DriftSwarm(Disc(4f), new List<DriftBody> { new DriftBody(0f, 0f, TargetR) }, new DriftSwarmTuning(), 3);
            var travelled = 0f;
            swarm.GetTarget(0, out var px, out var py, out _, out _);
            for (var i = 0; i < 600; i++)
            {
                swarm.Step(Dt);
                swarm.GetTarget(0, out var x, out var y, out _, out _);
                travelled += (float)Math.Sqrt((x - px) * (x - px) + (y - py) * (y - py));
                px = x;
                py = y;
            }

            Assert.That(travelled, Is.GreaterThan(swarm.Tile * 4f));
        }

        [Test]
        public void TargetsStayInsideTheSilhouette()
        {
            var targets = new List<DriftBody>
            {
                new DriftBody(-1.5f, 0f, TargetR),
                new DriftBody(1.5f, 0f, TargetR),
                new DriftBody(0f, 1.5f, TargetR)
            };
            var swarm = new DriftSwarm(Disc(4f), targets, new DriftSwarmTuning(), 4);
            for (var i = 0; i < 60 * 60; i++)
            {
                swarm.Step(Dt);
                if (i % 30 != 0)
                    continue;
                for (var t = 0; t < swarm.TargetCount; t++)
                {
                    swarm.GetTarget(t, out var x, out var y, out _, out _);
                    Assert.That(x * x + y * y, Is.LessThan(4f * 4f), "target " + t + " left the body at step " + i);
                }
            }
        }

        /// <summary>Loosely packed tiles (gaps wider than tiles, like real levels) in two lobes joined by a neck.</summary>
        static List<DriftBody> SparseDumbbell()
        {
            var bodies = new List<DriftBody>();
            var rng = new Random(3);
            for (var y = -3f; y <= 3f; y += 0.34f)
            {
                for (var x = -7f; x <= 7f; x += 0.34f)
                {
                    var jx = x + ((float)rng.NextDouble() - 0.5f) * 0.12f;
                    var jy = y + ((float)rng.NextDouble() - 0.5f) * 0.12f;
                    var lobe = Math.Min((jx + 4f) * (jx + 4f), (jx - 4f) * (jx - 4f)) + jy * jy < 2.6f * 2.6f;
                    var neck = Math.Abs(jx) < 4f && Math.Abs(jy) < 0.7f;
                    if ((lobe || neck) && rng.NextDouble() > 0.2)
                        bodies.Add(new DriftBody(jx, jy, 0.07f));
                }
            }

            return bodies;
        }

        [Test]
        public void TargetRoamsAcrossTheWholeBody()
        {
            var swarm = new DriftSwarm(SparseDumbbell(), new List<DriftBody> { new DriftBody(-4f, 0f, TargetR) }, new DriftSwarmTuning(), 6);
            var reachedFarLobe = false;
            for (var i = 0; i < 60 * 120 && !reachedFarLobe; i++)
            {
                swarm.Step(Dt);
                swarm.GetTarget(0, out var x, out var y, out _, out _);
                Assert.That(swarm.Field.Contains(x, y), Is.True, "target left the silhouette at step " + i);
                reachedFarLobe = x > 3f;
            }

            Assert.That(reachedFarLobe, Is.True, "target should travel through the neck into the other lobe");
        }

        [Test]
        public void SparseTilesStillFormOneBody()
        {
            var swarm = new DriftSwarm(SparseDumbbell(), new List<DriftBody>(), new DriftSwarmTuning(), 1);
            var cost = new float[swarm.Field.CellCount];
            Assert.That(swarm.Field.BuildFlow(-4f, 0f, 0.2f, cost), Is.True);
            Assert.That(swarm.Field.FlowAt(4f, 0f, cost), Is.LessThan(float.PositiveInfinity));
            Assert.That(swarm.Field.Contains(0f, 2f), Is.False, "gap beside the neck must stay outside");
        }

        [Test]
        public void RemovedTargetLetsTheCrowdClose()
        {
            var swarm = new DriftSwarm(Disc(4f), new List<DriftBody> { new DriftBody(0f, 0f, TargetR) }, new DriftSwarmTuning(), 5);
            swarm.Prewarm(2f);
            Assert.That(MaxHomeOffset(swarm), Is.GreaterThan(FillerR));
            swarm.RemoveTarget(0);
            Assert.That(swarm.IsTargetAlive(0), Is.False);
            for (var i = 0; i < 240; i++)
                swarm.Step(Dt);
            Assert.That(MaxHomeOffset(swarm), Is.LessThan(FillerR * 0.2f));
        }

        [Test]
        public void SameSeedSameMotion()
        {
            DriftSwarm Run()
            {
                var s = new DriftSwarm(Disc(3f), new List<DriftBody> { new DriftBody(0f, 0f, TargetR) }, new DriftSwarmTuning(), 9);
                for (var i = 0; i < 120; i++)
                    s.Step(Dt);
                return s;
            }

            Run().GetTarget(0, out var ax, out var ay, out _, out _);
            Run().GetTarget(0, out var bx, out var by, out _, out _);
            Assert.That(ax, Is.EqualTo(bx));
            Assert.That(ay, Is.EqualTo(by));
        }

        [Test]
        public void FieldKnowsInsideFromOutside()
        {
            var swarm = new DriftSwarm(Disc(3f), new List<DriftBody>(), new DriftSwarmTuning(), 1);
            Assert.That(swarm.Field.Contains(0f, 0f), Is.True);
            Assert.That(swarm.Field.Contains(5f, 0f), Is.False);
            Assert.That(swarm.Field.Depth(0f, 0f), Is.GreaterThan(swarm.Field.Depth(2.5f, 0f)));
            swarm.Field.Inward(2.5f, 0f, out var dx, out _);
            Assert.That(dx, Is.LessThan(0f));
        }
    }

    [TestFixture]
    public class DecoyFillTests
    {
        static readonly string[] Pool = { "apple", "bell", "cat", "drum", "egg", "fan" };

        [Test]
        public void ConvertsRequestedShare()
        {
            var result = DecoyFill.Assign(200, Pool, new HashSet<string>(), 35f, 7);
            var count = 0;
            foreach (var name in result)
            {
                if (name != null)
                    count++;
            }

            Assert.That(count, Is.EqualTo(70).Within(1));
        }

        [Test]
        public void NeverUsesHuntNames()
        {
            var exclude = new HashSet<string> { "cat", "egg" };
            var result = DecoyFill.Assign(300, Pool, exclude, 100f, 3);
            foreach (var name in result)
            {
                Assert.That(name, Is.Not.Null);
                Assert.That(exclude.Contains(name), Is.False);
            }
        }

        [Test]
        public void ZeroPercentOrEmptyPoolKeepsFillers()
        {
            Assert.That(DecoyFill.Assign(50, Pool, null, 0f, 1), Has.All.Null);
            Assert.That(DecoyFill.Assign(50, new[] { "cat" }, new HashSet<string> { "cat" }, 50f, 1), Has.All.Null);
        }

        [Test]
        public void DeterministicPerSeed()
        {
            Assert.That(DecoyFill.Assign(100, Pool, null, 40f, 11), Is.EqualTo(DecoyFill.Assign(100, Pool, null, 40f, 11)));
            Assert.That(DecoyFill.Assign(100, Pool, null, 40f, 11), Is.Not.EqualTo(DecoyFill.Assign(100, Pool, null, 40f, 12)));
        }
    }
}
