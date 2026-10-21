using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;

namespace Game.Core.UnitTests
{
    public sealed class RevealRoadmapTests
    {
        [Test]
        public void ImageHogWithRevealSetIsNotPlaceholder()
        {
            Assert.That(new ImageHogDef("garden", "Moonlit Garden", revealSet: "garden").HasRevealSet, Is.True);

            var levels = new List<HuntLevelDef>();
            for (var i = 0; i < 30; i++)
                levels.Add(new HuntLevelDef("m" + i + ".json", "M" + i));
            var hogs = new List<ImageHogDef>();
            foreach (var id in new[] { "garden", "forest", "newyork", "pandora", "mib", "matrix" })
                hogs.Add(new ImageHogDef(id, id, revealSet: id));

            var nodes = HuntRoadmap.Build(new HuntCatalog(levels, hogs), 5);

            Assert.That(nodes.Count, Is.EqualTo(36));
            Assert.That(HuntRoadmap.ChapterCount(nodes), Is.EqualTo(6));
            Assert.That(nodes[5].Kind, Is.EqualTo(HuntNodeKind.ImageHog));
            Assert.That(nodes[5].ImageHog.RevealSet, Is.EqualTo("garden"));
            Assert.That(nodes[5].IsPlaceholder, Is.False);
            Assert.That(nodes[35].ImageHog.RevealSet, Is.EqualTo("matrix"));
        }

        [Test]
        public void ImageHogWithoutSceneOrRevealSetIsPlaceholder()
        {
            var levels = new List<HuntLevelDef>();
            for (var i = 0; i < 5; i++)
                levels.Add(new HuntLevelDef("m" + i + ".json", "M" + i));
            var nodes = HuntRoadmap.Build(new HuntCatalog(levels, new[] { new ImageHogDef("c1", "Photo Hunt") }), 5);
            Assert.That(nodes[5].IsPlaceholder, Is.True);
        }
    }
}
