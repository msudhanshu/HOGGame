using Game.Core;
using Nixin.Mosaic.Core;
using UnityEngine;

namespace Game.Unity
{
    /// <summary>
    /// Exact outline of a reveal object (from the authoring tool's objects.json). The PolygonCollider2D on the
    /// same GameObject is the broad phase for Physics2D queries; this answers "is this point really on the
    /// object?" in image pixels, holes included, so neighbours never steal a tap.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RevealHitShape : MonoBehaviour
    {
        RevealObjectDef _def;
        int _imageWidth;
        int _imageHeight;
        float _pixelsPerUnit = MosaicCoords.DefaultPixelsPerUnit;

        public RevealObjectDef Def => _def;
        /// <summary>Outline area in image px² — smaller wins when shapes overlap the same point.</summary>
        public float AreaPx { get; private set; }

        public void Init(RevealObjectDef def, int imageWidth, int imageHeight, float pixelsPerUnit)
        {
            _def = def;
            _imageWidth = imageWidth;
            _imageHeight = imageHeight;
            _pixelsPerUnit = pixelsPerUnit;
            AreaPx = def.HasOutline ? RevealGeometry.Area(def.Paths) : def.Width * def.Height;
        }

        /// <summary>World point → image pixel (top-left origin) through the level root's transform.</summary>
        public Vector2 WorldToImage(Vector2 world)
        {
            var root = transform.parent;
            var local = root != null ? (Vector2)root.InverseTransformPoint(world) : world;
            MosaicCoords.ToImage(local.x, local.y, _imageWidth, _imageHeight, _pixelsPerUnit, out var px, out var py);
            return new Vector2(px, py);
        }

        public bool ContainsWorld(Vector2 world)
        {
            if (_def == null)
                return false;
            var p = WorldToImage(world);
            if (_def.HasOutline)
                return RevealGeometry.Contains(_def.Paths, p.x, p.y);
            return p.x >= _def.X && p.y >= _def.Y && p.x <= _def.X + _def.Width && p.y <= _def.Y + _def.Height;
        }
    }
}
