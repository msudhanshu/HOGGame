using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Walkable area of a mosaic silhouette. Levels carry no mask, so the shape is rasterised from the
    /// tiles' discs into a grid, then a chamfer distance-to-edge gives "how deep inside" every cell is.
    /// </summary>
    public sealed class SilhouetteField
    {
        readonly float _minX;
        readonly float _minY;
        readonly float _cell;
        readonly int _w;
        readonly int _h;
        readonly float[] _depth;
        readonly List<int> _deepCells = new List<int>();

        public SilhouetteField(IReadOnlyList<float> xs, IReadOnlyList<float> ys, IReadOnlyList<float> radii, float cellSize)
        {
            if (xs == null || ys == null || radii == null || xs.Count == 0)
                throw new ArgumentException("Need at least one tile.");
            _cell = cellSize > 0.0001f ? cellSize : 0.1f;

            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (var i = 0; i < xs.Count; i++)
            {
                minX = Math.Min(minX, xs[i] - radii[i]);
                minY = Math.Min(minY, ys[i] - radii[i]);
                maxX = Math.Max(maxX, xs[i] + radii[i]);
                maxY = Math.Max(maxY, ys[i] + radii[i]);
            }

            // Mosaics are loosely packed (gaps wider than tiles), so the body is a morphological closing of the
            // tile discs: grow every disc by the typical gap, then shrink the union back by the same amount.
            Closing = EstimateGap(xs, ys, radii);

            // One empty border ring so the edge always has an outside.
            var pad = _cell * 2f + Closing;
            _minX = minX - pad;
            _minY = minY - pad;
            _w = (int)Math.Ceiling((maxX + pad - _minX) / _cell) + 1;
            _h = (int)Math.Ceiling((maxY + pad - _minY) / _cell) + 1;
            var inside = new bool[_w * _h];
            for (var i = 0; i < xs.Count; i++)
                Stamp(inside, xs[i], ys[i], radii[i] * 1.15f + _cell * 0.5f + Closing);
            CloseHoles(inside);

            _depth = new float[_w * _h];
            Chamfer(inside);
            if (Closing > 0f)
            {
                for (var i = 0; i < inside.Length; i++)
                    inside[i] = _depth[i] > Closing;
                Chamfer(inside);
            }

            for (var i = 0; i < _depth.Length; i++)
                MaxDepth = Math.Max(MaxDepth, _depth[i]);
        }

        public float CellSize => _cell;
        /// <summary>Gap bridged when inferring the body from the tiles (world units).</summary>
        public float Closing { get; }
        public int CellCount => _depth.Length;
        /// <summary>Depth of the most interior cell: half the width of the body's thickest part.</summary>
        public float MaxDepth { get; }
        /// <summary>Diagonal of the silhouette's bounding box.</summary>
        public float Span => (float)Math.Sqrt(_w * _w + _h * _h) * _cell;
        /// <summary>Depth at which a point counts as inside for routing and movement.</summary>
        public float InsideDepth => _cell * 0.5f;

        const float ThinPenalty = 6f;

        public bool Contains(float x, float y) => Depth(x, y) > 0f;

        /// <summary>World distance to the nearest outside cell (0 outside), bilinear between cells.</summary>
        public float Depth(float x, float y)
        {
            var gx = (x - _minX) / _cell - 0.5f;
            var gy = (y - _minY) / _cell - 0.5f;
            var x0 = (int)Math.Floor(gx);
            var y0 = (int)Math.Floor(gy);
            var fx = gx - x0;
            var fy = gy - y0;
            var a = At(x0, y0) + (At(x0 + 1, y0) - At(x0, y0)) * fx;
            var b = At(x0, y0 + 1) + (At(x0 + 1, y0 + 1) - At(x0, y0 + 1)) * fx;
            return a + (b - a) * fy;
        }

        /// <summary>Unit direction of increasing depth (toward the silhouette's interior).</summary>
        public void Inward(float x, float y, out float dx, out float dy)
        {
            var e = _cell;
            dx = Depth(x + e, y) - Depth(x - e, y);
            dy = Depth(x, y + e) - Depth(x, y - e);
            var len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-5f)
            {
                dx = 0f;
                dy = 0f;
                return;
            }

            dx /= len;
            dy /= len;
        }

        /// <summary>
        /// Path cost from every inside cell to (goalX, goalY) (Dijkstra, 8-way). Cells near the edge cost more and
        /// cells thinner than comfortDepth cost a lot more, so routes run down the middle of the body and only
        /// squeeze through thin parts when they must. Unreachable cells are +infinity.
        /// </summary>
        public bool BuildFlow(float goalX, float goalY, float comfortDepth, float[] cost)
        {
            if (cost == null || cost.Length != _depth.Length)
                throw new ArgumentException("cost must have CellCount entries.", nameof(cost));
            for (var i = 0; i < cost.Length; i++)
                cost[i] = float.PositiveInfinity;
            var minDepth = InsideDepth;
            var comfort = Math.Max(comfortDepth, minDepth + 1e-4f);
            var goal = CellIndex(goalX, goalY);
            if (goal < 0 || _depth[goal] < minDepth)
                return false;

            _heap.Clear();
            cost[goal] = 0f;
            Push(goal, 0f);
            while (_heap.Count > 0)
            {
                Pop(out var cell, out var c);
                if (c > cost[cell])
                    continue;
                var cx = cell % _w;
                var cy = cell / _w;
                for (var k = 0; k < 8; k++)
                {
                    var nx = cx + NeighbourX[k];
                    var ny = cy + NeighbourY[k];
                    if (nx < 0 || ny < 0 || nx >= _w || ny >= _h)
                        continue;
                    var n = ny * _w + nx;
                    var d = _depth[n];
                    if (d < minDepth)
                        continue;
                    var edge = _cell / (d - minDepth + _cell);
                    var thin = d < comfort ? ThinPenalty * (comfort - d) / comfort : 0f;
                    var step = (k < 4 ? 1f : 1.4142f) * _cell * (1f + edge + thin);
                    var next = c + step;
                    if (next >= cost[n])
                        continue;
                    cost[n] = next;
                    Push(n, next);
                }
            }

            return true;
        }

        /// <summary>Cost-map value of the cell containing (x, y); +infinity outside the grid or unreachable.</summary>
        public float FlowAt(float x, float y, float[] cost)
        {
            var i = CellIndex(x, y);
            return i < 0 ? float.PositiveInfinity : cost[i];
        }

        /// <summary>Random cell centre at least minDepth inside, or false when the body is thinner than that.</summary>
        public bool RandomWalkable(Random rng, float minDepth, out float x, out float y)
        {
            _deepCells.Clear();
            for (var i = 0; i < _depth.Length; i++)
            {
                if (_depth[i] >= minDepth)
                    _deepCells.Add(i);
            }

            x = 0f;
            y = 0f;
            if (_deepCells.Count == 0)
                return false;
            var pick = _deepCells[rng.Next(_deepCells.Count)];
            x = _minX + (pick % _w + 0.5f) * _cell;
            y = _minY + (pick / _w + 0.5f) * _cell;
            return true;
        }

        int CellIndex(float x, float y)
        {
            var cx = (int)Math.Floor((x - _minX) / _cell);
            var cy = (int)Math.Floor((y - _minY) / _cell);
            if (cx < 0 || cy < 0 || cx >= _w || cy >= _h)
                return -1;
            return cy * _w + cx;
        }

        static readonly int[] NeighbourX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        static readonly int[] NeighbourY = { 0, 0, 1, -1, 1, -1, 1, -1 };
        readonly List<KeyValuePair<float, int>> _heap = new List<KeyValuePair<float, int>>();

        void Push(int cell, float cost)
        {
            _heap.Add(new KeyValuePair<float, int>(cost, cell));
            var i = _heap.Count - 1;
            while (i > 0)
            {
                var parent = (i - 1) / 2;
                if (_heap[parent].Key <= _heap[i].Key)
                    break;
                (_heap[parent], _heap[i]) = (_heap[i], _heap[parent]);
                i = parent;
            }
        }

        void Pop(out int cell, out float cost)
        {
            cell = _heap[0].Value;
            cost = _heap[0].Key;
            var last = _heap.Count - 1;
            _heap[0] = _heap[last];
            _heap.RemoveAt(last);
            var i = 0;
            while (true)
            {
                var l = i * 2 + 1;
                var r = l + 1;
                var m = i;
                if (l < _heap.Count && _heap[l].Key < _heap[m].Key) m = l;
                if (r < _heap.Count && _heap[r].Key < _heap[m].Key) m = r;
                if (m == i)
                    break;
                (_heap[m], _heap[i]) = (_heap[i], _heap[m]);
                i = m;
            }
        }

        /// <summary>
        /// Typical empty space between neighbouring tiles: 90th-percentile nearest-neighbour centre distance,
        /// halved, plus a cell (so the closing also seals the sparsest patches of the body).
        /// </summary>
        float EstimateGap(IReadOnlyList<float> xs, IReadOnlyList<float> ys, IReadOnlyList<float> radii)
        {
            var n = xs.Count;
            if (n < 3)
                return 0f;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (var i = 0; i < n; i++)
            {
                minX = Math.Min(minX, xs[i]);
                minY = Math.Min(minY, ys[i]);
                maxX = Math.Max(maxX, xs[i]);
                maxY = Math.Max(maxY, ys[i]);
            }

            var bucket = Math.Max(1e-3f, (float)Math.Sqrt((maxX - minX) * (maxY - minY) / n) * 2f);
            var cols = (int)((maxX - minX) / bucket) + 1;
            var rows = (int)((maxY - minY) / bucket) + 1;
            var buckets = new Dictionary<int, List<int>>();
            for (var i = 0; i < n; i++)
            {
                var key = (int)((ys[i] - minY) / bucket) * cols + (int)((xs[i] - minX) / bucket);
                if (!buckets.TryGetValue(key, out var list))
                    buckets[key] = list = new List<int>();
                list.Add(i);
            }

            var nearest = new List<float>(n);
            for (var i = 0; i < n; i++)
            {
                var bx = (int)((xs[i] - minX) / bucket);
                var by = (int)((ys[i] - minY) / bucket);
                var best = float.MaxValue;
                for (var y = Math.Max(0, by - 1); y <= Math.Min(rows - 1, by + 1); y++)
                {
                    for (var x = Math.Max(0, bx - 1); x <= Math.Min(cols - 1, bx + 1); x++)
                    {
                        if (!buckets.TryGetValue(y * cols + x, out var list))
                            continue;
                        foreach (var j in list)
                        {
                            if (j == i)
                                continue;
                            var dx = xs[j] - xs[i];
                            var dy = ys[j] - ys[i];
                            best = Math.Min(best, dx * dx + dy * dy);
                        }
                    }
                }

                if (best < float.MaxValue)
                    nearest.Add((float)Math.Sqrt(best) - radii[i]);
            }

            if (nearest.Count == 0)
                return 0f;
            nearest.Sort();
            return Math.Max(0f, nearest[(int)(nearest.Count * 0.9f)] * 0.5f) + _cell;
        }

        float At(int x, int y)
        {
            if (x < 0 || y < 0 || x >= _w || y >= _h)
                return 0f;
            return _depth[y * _w + x];
        }

        void Stamp(bool[] inside, float cx, float cy, float r)
        {
            var x0 = Math.Max(0, (int)((cx - r - _minX) / _cell));
            var x1 = Math.Min(_w - 1, (int)((cx + r - _minX) / _cell));
            var y0 = Math.Max(0, (int)((cy - r - _minY) / _cell));
            var y1 = Math.Min(_h - 1, (int)((cy + r - _minY) / _cell));
            var r2 = r * r;
            for (var y = y0; y <= y1; y++)
            {
                var py = _minY + (y + 0.5f) * _cell - cy;
                for (var x = x0; x <= x1; x++)
                {
                    var px = _minX + (x + 0.5f) * _cell - cx;
                    if (px * px + py * py <= r2)
                        inside[y * _w + x] = true;
                }
            }
        }

        /// <summary>Fills pinholes between packed discs (cells with 3+ inside 4-neighbours).</summary>
        void CloseHoles(bool[] inside)
        {
            for (var y = 1; y < _h - 1; y++)
            {
                for (var x = 1; x < _w - 1; x++)
                {
                    var i = y * _w + x;
                    if (inside[i])
                        continue;
                    var n = (inside[i - 1] ? 1 : 0) + (inside[i + 1] ? 1 : 0) + (inside[i - _w] ? 1 : 0) + (inside[i + _w] ? 1 : 0);
                    if (n >= 3)
                        inside[i] = true;
                }
            }
        }

        void Chamfer(bool[] inside)
        {
            const float big = 1e9f;
            var d1 = _cell;
            var d2 = _cell * 1.4142f;
            for (var i = 0; i < _depth.Length; i++)
                _depth[i] = inside[i] ? big : 0f;

            for (var y = 0; y < _h; y++)
            {
                for (var x = 0; x < _w; x++)
                {
                    var i = y * _w + x;
                    if (_depth[i] == 0f)
                        continue;
                    var v = _depth[i];
                    if (x > 0) v = Math.Min(v, _depth[i - 1] + d1);
                    if (y > 0) v = Math.Min(v, _depth[i - _w] + d1);
                    if (x > 0 && y > 0) v = Math.Min(v, _depth[i - _w - 1] + d2);
                    if (x < _w - 1 && y > 0) v = Math.Min(v, _depth[i - _w + 1] + d2);
                    _depth[i] = v;
                }
            }

            for (var y = _h - 1; y >= 0; y--)
            {
                for (var x = _w - 1; x >= 0; x--)
                {
                    var i = y * _w + x;
                    if (_depth[i] == 0f)
                        continue;
                    var v = _depth[i];
                    if (x < _w - 1) v = Math.Min(v, _depth[i + 1] + d1);
                    if (y < _h - 1) v = Math.Min(v, _depth[i + _w] + d1);
                    if (x < _w - 1 && y < _h - 1) v = Math.Min(v, _depth[i + _w + 1] + d2);
                    if (x > 0 && y < _h - 1) v = Math.Min(v, _depth[i + _w - 1] + d2);
                    _depth[i] = v;
                }
            }
        }
    }
}
