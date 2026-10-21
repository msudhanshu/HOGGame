using Game.Core;
using Game.Unity;
using Nixin.Mosaic.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Unity.EditTests
{
    /// <summary>objects.json outlines → PolygonCollider2D + RevealHitShape agree with the authored pixels.</summary>
    public sealed class RevealOutlineTests
    {
        const int W = 400;
        const int H = 300;
        const float Ppu = MosaicCoords.DefaultPixelsPerUnit;

        const string Json = @"{""width"":400,""height"":300,""background"":""#000000"",""objects"":[
            {""name"":""tri"",""x"":100,""y"":100,""w"":100,""h"":100,
             ""paths"":[{""hole"":false,""points"":[100,100,200,100,100,200]}],
             ""icon"":""icons/tri.png"",""icon_x"":96,""icon_y"":96,""pad"":2,
             ""seeds"":[{""x"":120,""y"":120,""positive"":true},{""x"":190,""y"":190,""positive"":false}]},
            {""name"":""box"",""x"":10,""y"":10,""w"":20,""h"":20}]}";

        GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.DestroyImmediate(_root);
        }

        static Vector2 World(float px, float py)
        {
            MosaicCoords.ToWorld(px, py, W, H, Ppu, out var wx, out var wy);
            return new Vector2(wx, wy);
        }

        [Test]
        public void ParseAndToJsonRoundTripOutlinesIconsAndSeeds()
        {
            var set = RevealLevelBuilder.Parse("t", Json);
            var tri = set.Objects[0];
            Assert.That(tri.HasOutline, Is.True);
            Assert.That(tri.Paths[0].VertexCount, Is.EqualTo(3));
            Assert.That((tri.Icon, tri.IconX, tri.IconY, tri.Pad), Is.EqualTo(("icons/tri.png", 96, 96, 2)));
            Assert.That(tri.Seeds[1].Positive, Is.False);
            Assert.That(set.Objects[1].HasOutline, Is.False);
            Assert.That(RevealSetValidator.Validate(set), Is.Empty);

            var again = RevealLevelBuilder.Parse("t", RevealLevelBuilder.ToJson(set));
            Assert.That(again.Objects[0].Paths[0].Points, Is.EqualTo(tri.Paths[0].Points));
            Assert.That(again.Objects[0].Seeds.Count, Is.EqualTo(2));
            Assert.That(again.Objects[1].HasOutline, Is.False);
        }

        [Test]
        public void PolygonColliderMatchesOutlineNotBoundingBox()
        {
            var tri = RevealLevelBuilder.Parse("t", Json).Objects[0];
            _root = new GameObject("LevelRoot");
            var go = new GameObject("tri");
            go.transform.SetParent(_root.transform, false);
            var anchor = new Vector2(tri.X + tri.Width * 0.5f, tri.Y + tri.Height * 0.5f);
            go.transform.localPosition = World(anchor.x, anchor.y);
            var poly = go.AddComponent<PolygonCollider2D>();
            poly.pathCount = 1;
            poly.SetPath(0, RevealLevelBuilder.LocalPath(tri.Paths[0], anchor, Vector2.one));
            var shape = go.AddComponent<RevealHitShape>();
            shape.Init(tri, W, H, Ppu);
            Physics2D.SyncTransforms();

            var inside = World(120, 120); // near the right-angle corner
            var corner = World(190, 190); // inside the bounding box, outside the triangle
            Assert.That(poly.OverlapPoint(inside), Is.True);
            Assert.That(poly.OverlapPoint(corner), Is.False);
            Assert.That(shape.ContainsWorld(inside), Is.True);
            Assert.That(shape.ContainsWorld(corner), Is.False);
            Assert.That(shape.WorldToImage(inside).x, Is.EqualTo(120f).Within(0.01f));
            Assert.That(shape.AreaPx, Is.EqualTo(5000f).Within(0.5f));
        }
    }
}
