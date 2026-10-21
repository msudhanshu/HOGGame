using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// One closed outline of an object, in image pixels (top-left origin, y down) as a flat x0,y0,x1,y1,… list.
    /// Hole paths cut out of the outer paths (e.g. the inside of a wheel).
    /// </summary>
    public sealed class RevealPathDef
    {
        public RevealPathDef(bool hole, IReadOnlyList<float> points)
        {
            Hole = hole;
            Points = points ?? Array.Empty<float>();
        }

        public bool Hole { get; }
        public IReadOnlyList<float> Points { get; }
        public int VertexCount => Points.Count / 2;
    }

    /// <summary>A marker / hint the authoring tool used to find the object (kept so re-detect works).</summary>
    public readonly struct RevealSeedDef
    {
        public RevealSeedDef(int x, int y, bool positive)
        {
            X = x;
            Y = y;
            Positive = positive;
        }

        public int X { get; }
        public int Y { get; }
        public bool Positive { get; }
    }

    /// <summary>
    /// One object painted into a reveal level's day image. Pixel rect (bounding box), top-left origin.
    /// Optional <see cref="Paths"/> give the exact outline; without them the rect is the hit shape.
    /// </summary>
    public sealed class RevealObjectDef
    {
        public RevealObjectDef(string name, int x, int y, int width, int height,
            IReadOnlyList<RevealPathDef> paths = null, string icon = null, int pad = 0,
            IReadOnlyList<RevealSeedDef> seeds = null, int iconX = -1, int iconY = -1)
        {
            Name = name ?? string.Empty;
            X = x;
            Y = y;
            Width = width;
            Height = height;
            Paths = paths ?? Array.Empty<RevealPathDef>();
            Icon = icon ?? string.Empty;
            Pad = pad;
            Seeds = seeds ?? Array.Empty<RevealSeedDef>();
            IconX = iconX;
            IconY = iconY;
        }

        public string Name { get; }
        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<RevealPathDef> Paths { get; }
        /// <summary>Cutout icon relative to the set folder (e.g. "icons/cat.png"); empty = crop the rect.</summary>
        public string Icon { get; }
        /// <summary>Top-left of the icon in image pixels (the cutout is laid back exactly there); -1 = unknown.</summary>
        public int IconX { get; }
        public int IconY { get; }
        public bool HasIconPlacement => Icon.Length > 0 && IconX >= 0 && IconY >= 0;
        /// <summary>Pixels the authoring tool grew the outline by so thin objects stay tappable.</summary>
        public int Pad { get; }
        public IReadOnlyList<RevealSeedDef> Seeds { get; }
        public bool HasOutline => Paths.Count > 0;

        public RevealObjectDef WithName(string name) =>
            new RevealObjectDef(name, X, Y, Width, Height, Paths, Icon, Pad, Seeds, IconX, IconY);

        /// <summary>Moving / resizing the rect by hand drops the outline (it no longer matches).</summary>
        public RevealObjectDef WithRect(int x, int y, int width, int height) =>
            new RevealObjectDef(Name, x, y, width, height, null, Icon, Pad, Seeds, IconX, IconY);

        /// <summary>Shift rect and outline together (the icon stays where it was cut from).</summary>
        public RevealObjectDef Translated(int dx, int dy)
        {
            if (Paths.Count == 0)
                return new RevealObjectDef(Name, X + dx, Y + dy, Width, Height, null, Icon, Pad, Seeds, IconX, IconY);
            var moved = new RevealPathDef[Paths.Count];
            for (var i = 0; i < Paths.Count; i++)
            {
                var src = Paths[i].Points;
                var pts = new float[src.Count];
                for (var k = 0; k + 1 < src.Count; k += 2)
                {
                    pts[k] = src[k] + dx;
                    pts[k + 1] = src[k + 1] + dy;
                }

                moved[i] = new RevealPathDef(Paths[i].Hole, pts);
            }

            return new RevealObjectDef(Name, X + dx, Y + dy, Width, Height, moved, Icon, Pad, Seeds, IconX, IconY);
        }

        /// <summary>New outline; the rect becomes its bounding box. Null/empty = rect only.</summary>
        public RevealObjectDef WithPaths(IReadOnlyList<RevealPathDef> paths)
        {
            if (paths == null || paths.Count == 0)
                return new RevealObjectDef(Name, X, Y, Width, Height, null, Icon, Pad, Seeds, IconX, IconY);
            RevealGeometry.Bounds(paths, out var x, out var y, out var w, out var h);
            return new RevealObjectDef(Name, x, y, w, h, paths, Icon, Pad, Seeds, IconX, IconY);
        }
    }

    /// <summary>A day/night image pair plus the hidden objects painted into the day image.</summary>
    public sealed class RevealSetDef
    {
        public RevealSetDef(string id, int width, int height, IReadOnlyList<RevealObjectDef> objects, string background = null)
        {
            Id = id ?? string.Empty;
            Width = width;
            Height = height;
            Objects = objects ?? Array.Empty<RevealObjectDef>();
            Background = background ?? string.Empty;
        }

        public string Id { get; }
        public int Width { get; }
        public int Height { get; }
        public string Background { get; }
        public IReadOnlyList<RevealObjectDef> Objects { get; }

        public List<string> ObjectNames()
        {
            var names = new List<string>(Objects.Count);
            for (var i = 0; i < Objects.Count; i++)
                names.Add(Objects[i].Name);
            return names;
        }
    }

    public static class RevealSetValidator
    {
        /// <summary>Empty list = valid. requiredTargets = WaveSize * WaveCount of the level using the set.</summary>
        public static List<string> Validate(RevealSetDef set, int requiredTargets = 0)
        {
            var errors = new List<string>();
            if (set == null)
            {
                errors.Add("Reveal set is missing.");
                return errors;
            }

            if (set.Width <= 0 || set.Height <= 0)
                errors.Add("Image size must be positive.");

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < set.Objects.Count; i++)
            {
                var o = set.Objects[i];
                var label = string.IsNullOrWhiteSpace(o.Name) ? "#" + i : o.Name;
                if (string.IsNullOrWhiteSpace(o.Name))
                    errors.Add($"Object #{i} has no name.");
                else if (!seen.Add(o.Name))
                    errors.Add($"Duplicate object name '{o.Name}'.");
                if (o.Width <= 0 || o.Height <= 0)
                    errors.Add($"'{label}' has an empty rect.");
                if (o.X < 0 || o.Y < 0 || o.X + o.Width > set.Width || o.Y + o.Height > set.Height)
                    errors.Add($"'{label}' lies outside the image.");
                for (var p = 0; p < o.Paths.Count; p++)
                {
                    var pts = o.Paths[p].Points;
                    if (pts.Count % 2 != 0)
                        errors.Add($"'{label}' path {p} has an odd number of coordinates.");
                    else if (pts.Count < 6)
                        errors.Add($"'{label}' path {p} needs at least 3 points.");
                    for (var k = 0; k + 1 < pts.Count; k += 2)
                    {
                        if (pts[k] < 0 || pts[k + 1] < 0 || pts[k] > set.Width || pts[k + 1] > set.Height)
                        {
                            errors.Add($"'{label}' path {p} has a point outside the image.");
                            break;
                        }
                    }
                }

                if (o.Paths.Count > 0 && !HasOuter(o.Paths))
                    errors.Add($"'{label}' has only hole paths.");
            }

            if (requiredTargets > 0 && set.Objects.Count < requiredTargets)
                errors.Add($"Level needs {requiredTargets} objects, set has {set.Objects.Count}.");

            return errors;
        }

        static bool HasOuter(IReadOnlyList<RevealPathDef> paths)
        {
            for (var i = 0; i < paths.Count; i++)
            {
                if (!paths[i].Hole)
                    return true;
            }

            return false;
        }
    }

    /// <summary>Pure polygon helpers in image pixels (shared by the builder, editor and tests).</summary>
    public static class RevealGeometry
    {
        /// <summary>Inside any outer path and not inside a hole path.</summary>
        public static bool Contains(IReadOnlyList<RevealPathDef> paths, float x, float y)
        {
            var inOuter = false;
            for (var i = 0; i < paths.Count; i++)
            {
                if (!PolygonContains(paths[i].Points, x, y))
                    continue;
                if (paths[i].Hole)
                    return false;
                inOuter = true;
            }

            return inOuter;
        }

        /// <summary>Even-odd ray cast on a flat x,y list.</summary>
        public static bool PolygonContains(IReadOnlyList<float> pts, float x, float y)
        {
            var n = pts.Count / 2;
            var inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float xi = pts[2 * i], yi = pts[2 * i + 1], xj = pts[2 * j], yj = pts[2 * j + 1];
                if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi)
                    inside = !inside;
            }

            return inside;
        }

        /// <summary>Unsigned shoelace area of outer paths minus holes, in px².</summary>
        public static float Area(IReadOnlyList<RevealPathDef> paths)
        {
            var total = 0f;
            for (var i = 0; i < paths.Count; i++)
            {
                var a = Math.Abs(SignedArea(paths[i].Points));
                total += paths[i].Hole ? -a : a;
            }

            return Math.Max(0f, total);
        }

        public static float SignedArea(IReadOnlyList<float> pts)
        {
            var n = pts.Count / 2;
            var sum = 0f;
            for (int i = 0, j = n - 1; i < n; j = i++)
                sum += pts[2 * j] * pts[2 * i + 1] - pts[2 * i] * pts[2 * j + 1];
            return sum * 0.5f;
        }

        /// <summary>Integer pixel bounding box covering every path point.</summary>
        public static void Bounds(IReadOnlyList<RevealPathDef> paths, out int x, out int y, out int width, out int height)
        {
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (var i = 0; i < paths.Count; i++)
            {
                var pts = paths[i].Points;
                for (var k = 0; k + 1 < pts.Count; k += 2)
                {
                    minX = Math.Min(minX, pts[k]);
                    maxX = Math.Max(maxX, pts[k]);
                    minY = Math.Min(minY, pts[k + 1]);
                    maxY = Math.Max(maxY, pts[k + 1]);
                }
            }

            if (minX > maxX)
            {
                x = y = width = height = 0;
                return;
            }

            x = (int)Math.Floor(minX);
            y = (int)Math.Floor(minY);
            width = Math.Max(1, (int)Math.Ceiling(maxX) - x);
            height = Math.Max(1, (int)Math.Ceiling(maxY) - y);
        }
    }
}
