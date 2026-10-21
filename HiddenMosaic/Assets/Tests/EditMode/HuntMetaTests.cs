using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public sealed class HuntEconomyRulesTests
    {
        readonly HuntEconomyTuning _t = new HuntEconomyTuning();

        [Test]
        public void Stars_FastAndNoHints_IsThree() => Assert.AreEqual(3, HuntEconomyRules.StarsFor(0.6f, 0, _t));

        [Test]
        public void Stars_FastButHinted_IsTwo() => Assert.AreEqual(2, HuntEconomyRules.StarsFor(0.6f, 1, _t));

        [Test]
        public void Stars_Slow_IsOne() => Assert.AreEqual(1, HuntEconomyRules.StarsFor(0.1f, 0, _t));

        [Test]
        public void Coins_FirstClearThreeStars()
        {
            // (8 + 4*3) * 2 = 40
            Assert.AreEqual(40, HuntEconomyRules.CoinsForWin(3, 3, true, 0, _t));
        }

        [Test]
        public void Coins_ReplayNoImprovementIsToken()
        {
            Assert.AreEqual(_t.ReplayCoins, HuntEconomyRules.CoinsForWin(1, 2, false, 2, _t));
        }

        [Test]
        public void Coins_ReplayImprovementPaysDifference()
        {
            // t1: base 12 -> 1 star 12, 3 stars 24 -> +12
            Assert.AreEqual(12, HuntEconomyRules.CoinsForWin(1, 3, false, 1, _t));
        }

        [Test]
        public void PhotoFee_ScalesWithToughness()
        {
            Assert.AreEqual(65, HuntEconomyRules.PhotoFee(1, _t));
            Assert.AreEqual(125, HuntEconomyRules.PhotoFee(5, _t));
        }

        [Test]
        public void ContinueCost_Ramps()
        {
            Assert.AreEqual(30, HuntEconomyRules.ContinueCost(0, _t));
            Assert.AreEqual(45, HuntEconomyRules.ContinueCost(1, _t));
            Assert.AreEqual(60, HuntEconomyRules.ContinueCost(2, _t));
        }
    }

    public sealed class HuntRoadmapTests
    {
        static HuntCatalog Catalog(int mosaics, params ImageHogDef[] hogs)
        {
            var defs = new List<HuntLevelDef>();
            for (var i = 0; i < mosaics; i++)
                defs.Add(new HuntLevelDef(i % 2 == 0 ? "a.json" : "b.json", "L" + i));
            return new HuntCatalog(defs, hogs);
        }

        [Test]
        public void ImageHogAfterEveryFiveMosaics()
        {
            var nodes = HuntRoadmap.Build(Catalog(12, new ImageHogDef("kitchen", "Kitchen", "KitchenScene")), 5);
            // 5 + hog + 5 + hog + 2 = 14
            Assert.AreEqual(14, nodes.Count);
            Assert.AreEqual(HuntNodeKind.ImageHog, nodes[5].Kind);
            Assert.AreEqual("h:kitchen", nodes[5].Id);
            Assert.IsFalse(nodes[5].IsPlaceholder);
            Assert.AreEqual(HuntNodeKind.ImageHog, nodes[11].Kind);
            Assert.IsTrue(nodes[11].IsPlaceholder);
            Assert.AreEqual("h:chapter2", nodes[11].Id);
            Assert.AreEqual(HuntNodeKind.Mosaic, nodes[13].Kind);
            Assert.AreEqual(2, nodes[13].Chapter);
        }

        [Test]
        public void MosaicIdsAreUniqueForRepeatedFiles()
        {
            var nodes = HuntRoadmap.Build(Catalog(4), 5);
            var ids = new HashSet<string>();
            foreach (var n in nodes)
                Assert.IsTrue(ids.Add(n.Id), n.Id);
        }

        [Test]
        public void NodeIndexForCatalogIndexSkipsHogs()
        {
            var nodes = HuntRoadmap.Build(Catalog(8), 5);
            Assert.AreEqual(6, HuntRoadmap.NodeIndexForCatalogIndex(nodes, 5));
        }
    }

    public sealed class HuntProgressStateTests
    {
        [Test]
        public void KeepsBestStarsAndFlagsFirstClear()
        {
            var s = new HuntProgressState();
            Assert.IsTrue(s.RecordWin(0, "m:a#0", 2));
            Assert.IsFalse(s.RecordWin(0, "m:a#0", 1));
            Assert.AreEqual(2, s.StarsFor("m:a#0"));
            s.RecordWin(0, "m:a#0", 3);
            Assert.AreEqual(3, s.StarsFor("m:a#0"));
            Assert.AreEqual(1, s.HighestUnlocked);
        }

        [Test]
        public void RoundTripsSerialization()
        {
            var s = new HuntProgressState();
            s.RecordWin(0, "m:a#0", 3);
            s.RecordWin(1, "h:kitchen", 0);
            var copy = HuntProgressState.Deserialize(s.Serialize());
            Assert.AreEqual(2, copy.HighestUnlocked);
            Assert.AreEqual(3, copy.StarsFor("m:a#0"));
            Assert.IsTrue(copy.IsCleared("h:kitchen"));
            Assert.AreEqual(3, copy.TotalStars);
        }
    }

    public sealed class HuntLevelClockTests
    {
        [Test]
        public void AddTimeRestartsExpiredClock()
        {
            var c = new HuntLevelClock();
            c.Reset(10f);
            c.Tick(12f);
            Assert.IsTrue(c.IsExpired);
            c.AddTime(30f);
            Assert.IsFalse(c.IsExpired);
            Assert.AreEqual(30f, c.Remaining, 0.001f);
            Assert.IsTrue(c.IsRunning);
        }

        [Test]
        public void RemainingFraction()
        {
            var c = new HuntLevelClock();
            c.Reset(100f);
            c.Tick(25f);
            Assert.AreEqual(0.75f, c.RemainingFraction, 0.001f);
        }
    }
}
