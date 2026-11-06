using System;
using System.Collections.Generic;

namespace Game.Core
{
    [Serializable]
    public sealed class DriftSwarmTuning
    {
        /// <summary>Target cruise speed in target diameters per second; targets roam the whole body.</summary>
        public float Speed = 0.5f;
        /// <summary>Slowest a crowd can make a target, as a share of Speed.</summary>
        public float MinSpeedFraction = 0.55f;
        /// <summary>Max heading change, radians per second.</summary>
        public float TurnRate = 1.6f;
        /// <summary>Random heading drift, radians; keeps paths organic.</summary>
        public float Wander = 0.35f;
        /// <summary>How much shoving through a crowd slows a target down.</summary>
        public float CrowdSlowdown = 0.6f;
        /// <summary>Push contact distance, × (target radius + filler radius).</summary>
        public float PushReach = 1.05f;
        /// <summary>Share of the overlap resolved per substep (soft shove; final pass is hard).</summary>
        public float PushStiffness = 0.5f;
        /// <summary>0 = tiles ahead are shoved straight out, 1 = fully sideways (parting crowd).</summary>
        public float SidewaysBias = 0.75f;
        /// <summary>Spring pulling each filler back to its home slot.</summary>
        public float Spring = 16f;
        public float Damping = 6f;
        /// <summary>Share of a shove kept as velocity so it ripples and overshoots.</summary>
        public float Carry = 0.45f;
        /// <summary>Filler-filler overlap resolved per substep.</summary>
        public float RelaxStrength = 0.6f;
        /// <summary>Max filler distance from home, in its own radii.</summary>
        public float MaxOffset = 4f;
        /// <summary>Degrees of twist per tile/s of swirl as tiles are shoved and spring back.</summary>
        public float Wobble = 22f;
        /// <summary>Max scale loss of a squeezed filler.</summary>
        public float Squeeze = 0.14f;
        /// <summary>Degrees a target banks per rad/s of turn.</summary>
        public float TargetBank = 6f;
    }

    public struct DriftBody
    {
        public float X;
        public float Y;
        public float R;

        public DriftBody(float x, float y, float r)
        {
            X = x;
            Y = y;
            R = r;
        }
    }

    /// <summary>
    /// Fake physics for the drift hunt: targets steer through the silhouette and shove filler tiles aside
    /// (biased sideways like a bow wave); fillers are damped springs to their home slots, so the wake closes
    /// behind. Position-based contacts + one filler relaxation pass make the shove ripple outward.
    /// </summary>
    public sealed class DriftSwarm
    {
        const float HeatFadeSeconds = 0.8f;
        const float HeatSpread = 0.85f;
        const float StuckSeconds = 2.5f;
        const int LookDirections = 16;
        static readonly float[] SlideAngles = { 0f, 0.45f, -0.45f, 0.9f, -0.9f, 1.35f, -1.35f };

        struct Filler
        {
            public float HomeX, HomeY, X, Y, Vx, Vy, R;
            public float PreX, PreY, Push, Angle, Squash;
            /// <summary>1 right after a target shove, fading to 0; gates the filler-filler relaxation.</summary>
            public float Heat;
        }

        struct Target
        {
            public float X, Y, R, Heading, Speed, WanderOffset, WayX, WayY, Pressure, Bank, Bulge, TurnRate;
            public bool Alive;
            /// <summary>Route cost map to the current waypoint (per field cell).</summary>
            public float[] Flow;
            public float BestCost, Stuck;
        }

        readonly DriftSwarmTuning _t;
        readonly Random _rng;
        readonly Filler[] _f;
        readonly Target[] _g;
        readonly SilhouetteField _field;
        readonly float _tile;
        readonly float _maxFillerR;
        readonly float _maxTargetR;

        // Uniform grid (linked lists) over filler positions, rebuilt every substep.
        readonly float _gridMinX;
        readonly float _gridMinY;
        readonly float _gridCell;
        readonly int _gridW;
        readonly int _gridH;
        readonly int[] _head;
        readonly int[] _next;

        public DriftSwarm(IReadOnlyList<DriftBody> fillers, IReadOnlyList<DriftBody> targets, DriftSwarmTuning tuning, int seed)
        {
            if (fillers == null)
                throw new ArgumentNullException(nameof(fillers));
            if (targets == null)
                throw new ArgumentNullException(nameof(targets));
            _t = tuning ?? new DriftSwarmTuning();
            _rng = new Random(seed);

            _f = new Filler[fillers.Count];
            var diameters = new List<float>(fillers.Count);
            for (var i = 0; i < fillers.Count; i++)
            {
                var b = fillers[i];
                _f[i] = new Filler { HomeX = b.X, HomeY = b.Y, X = b.X, Y = b.Y, R = b.R };
                diameters.Add(b.R * 2f);
                _maxFillerR = Math.Max(_maxFillerR, b.R);
            }

            diameters.Sort();
            _tile = diameters.Count > 0 ? diameters[diameters.Count / 2] : 0f;

            _g = new Target[targets.Count];
            for (var i = 0; i < targets.Count; i++)
            {
                var b = targets[i];
                _g[i] = new Target
                {
                    X = b.X,
                    Y = b.Y,
                    R = b.R,
                    Alive = true,
                    Heading = (float)(_rng.NextDouble() * Math.PI * 2)
                };
                _maxTargetR = Math.Max(_maxTargetR, b.R);
                if (_tile <= 0f)
                    _tile = b.R;
            }

            if (_tile <= 0f)
                _tile = 0.1f;

            var xs = new List<float>(fillers.Count + targets.Count);
            var ys = new List<float>(xs.Capacity);
            var rs = new List<float>(xs.Capacity);
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (var i = 0; i < fillers.Count; i++)
                AddBounds(fillers[i]);
            for (var i = 0; i < targets.Count; i++)
                AddBounds(targets[i]);
            _field = new SilhouetteField(xs, ys, rs, Math.Max(_tile * 0.5f, _maxTargetR * 0.25f));

            for (var i = 0; i < _g.Length; i++)
                NewWaypoint(ref _g[i]);

            var pad = Math.Max(_maxFillerR, _tile) * (_t.MaxOffset + 2f);
            _gridCell = Math.Max(_maxFillerR * 2f, _tile);
            _gridMinX = minX - pad;
            _gridMinY = minY - pad;
            _gridW = Math.Max(1, (int)Math.Ceiling((maxX - minX + pad * 2f) / _gridCell));
            _gridH = Math.Max(1, (int)Math.Ceiling((maxY - minY + pad * 2f) / _gridCell));
            _head = new int[_gridW * _gridH];
            _next = new int[_f.Length];

            void AddBounds(DriftBody b)
            {
                xs.Add(b.X);
                ys.Add(b.Y);
                rs.Add(b.R);
                minX = Math.Min(minX, b.X);
                minY = Math.Min(minY, b.Y);
                maxX = Math.Max(maxX, b.X);
                maxY = Math.Max(maxY, b.Y);
            }
        }

        public int FillerCount => _f.Length;
        public int TargetCount => _g.Length;
        /// <summary>Median filler diameter.</summary>
        public float Tile => _tile;
        public SilhouetteField Field => _field;
        public float Time { get; private set; }

        public void GetFiller(int i, out float x, out float y, out float angleDeg, out float scale)
        {
            var f = _f[i];
            x = f.X;
            y = f.Y;
            angleDeg = f.Angle;
            scale = 1f - _t.Squeeze * f.Squash;
        }

        public float FillerHomeDistance(int i)
        {
            var dx = _f[i].X - _f[i].HomeX;
            var dy = _f[i].Y - _f[i].HomeY;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public void GetTarget(int i, out float x, out float y, out float angleDeg, out float scale)
        {
            var g = _g[i];
            x = g.X;
            y = g.Y;
            angleDeg = g.Bank;
            scale = 1f + g.Bulge;
        }

        public float TargetRadius(int i) => _g[i].R;
        public bool IsTargetAlive(int i) => _g[i].Alive;

        /// <summary>Shot down: stops pushing, so the crowd closes over its hole.</summary>
        public void RemoveTarget(int i)
        {
            _g[i].Alive = false;
        }

        /// <summary>Runs the sim without drawing so the opening frame already shows targets nested in the crowd.</summary>
        public void Prewarm(float seconds, float substep = 1f / 60f)
        {
            for (var t = 0f; t < seconds; t += substep)
                Step(substep);
        }

        public void Step(float dt)
        {
            if (dt <= 0f)
                return;
            Time += dt;
            for (var i = 0; i < _g.Length; i++)
            {
                if (_g[i].Alive)
                    SteerTarget(ref _g[i], i, dt);
            }

            IntegrateFillers(dt);
            RebuildGrid();
            PushFromTargets(_t.PushStiffness);
            Relax(dt);
            ClampOffsets();
            PushFromTargets(1f);
            SettleFillers(dt);
            SeparateTargets();
        }

        void SteerTarget(ref Target g, int index, float dt)
        {
            var walk = WalkDepth(g.R);
            var depth = _field.Depth(g.X, g.Y);
            var here = _field.FlowAt(g.X, g.Y, g.Flow);
            if (here < _maxTargetR * 3f)
                NewWaypoint(ref g);
            else if (here < g.BestCost - _field.CellSize * 0.5f)
            {
                g.BestCost = here;
                g.Stuck = 0f;
            }
            else if ((g.Stuck += dt) > StuckSeconds)
                NewWaypoint(ref g);

            // Follow the route: the lowest-cost spot one step ahead (route stays inside the body).
            float dx, dy;
            if (!PathDirection(ref g, out dx, out dy))
            {
                // Off the walkable core (spawned at the edge, or shoved): head for the interior.
                _field.Inward(g.X, g.Y, out dx, out dy);
                if (dx == 0f && dy == 0f)
                {
                    dx = (float)Math.Cos(g.Heading);
                    dy = (float)Math.Sin(g.Heading);
                }
            }

            // Keep targets from bunching up.
            for (var j = 0; j < _g.Length; j++)
            {
                if (j == index || !_g[j].Alive)
                    continue;
                var ox = g.X - _g[j].X;
                var oy = g.Y - _g[j].Y;
                var reach = (g.R + _g[j].R) * 2.5f;
                var d2 = ox * ox + oy * oy;
                if (d2 >= reach * reach || d2 < 1e-8f)
                    continue;
                var d = (float)Math.Sqrt(d2);
                var w = (reach - d) / reach;
                dx += ox / d * w;
                dy += oy / d * w;
            }

            g.WanderOffset += ((float)_rng.NextDouble() - 0.5f) * _t.Wander * 4f * dt;
            g.WanderOffset = Clamp(g.WanderOffset, -_t.Wander, _t.Wander);
            var desired = (float)Math.Atan2(dy, dx) + g.WanderOffset;
            var diff = WrapAngle(desired - g.Heading);
            var maxTurn = _t.TurnRate * dt;
            var turn = Clamp(diff, -maxTurn, maxTurn);
            g.Heading = WrapAngle(g.Heading + turn);

            // Shoving through the crowd slows a target, but never stalls it.
            var slow = Math.Max(_t.MinSpeedFraction, 1f / (1f + _t.CrowdSlowdown * g.Pressure / Math.Max(1e-4f, g.R)));
            var cruise = _t.Speed * _maxTargetR * 2f * slow;
            g.Speed += (cruise - g.Speed) * Clamp01(dt * 3f);

            // Step, sliding along the outline instead of crossing it.
            var step = g.Speed * dt;
            var moved = false;
            for (var k = 0; k < SlideAngles.Length && !moved; k++)
            {
                var h = g.Heading + SlideAngles[k];
                var nx = g.X + (float)Math.Cos(h) * step;
                var ny = g.Y + (float)Math.Sin(h) * step;
                var nd = _field.Depth(nx, ny);
                // Comfortably inside: free. Thin part: may not head further out. Never leave the outline.
                if (nd < walk * 0.8f && (nd < _field.InsideDepth || nd < depth - _field.CellSize * 0.05f))
                    continue;
                g.X = nx;
                g.Y = ny;
                if (k > 0)
                    g.Heading = WrapAngle(g.Heading + SlideAngles[k] * 0.5f);
                moved = true;
            }

            if (!moved)
                g.Speed *= 0.5f;

            g.TurnRate += (turn / dt - g.TurnRate) * Clamp01(dt * 4f);
            var bank = Clamp(-g.TurnRate * _t.TargetBank, -12f, 12f);
            g.Bank += (bank - g.Bank) * Clamp01(dt * 5f);
            var bulge = Math.Min(0.06f, g.Pressure / Math.Max(1e-4f, g.R) * 0.05f);
            g.Bulge += (bulge - g.Bulge) * Clamp01(dt * 8f);
            g.Pressure = 0f;
        }

        /// <summary>Unit direction toward the lowest route cost one lookahead away; false when off the route map.</summary>
        bool PathDirection(ref Target g, out float dx, out float dy)
        {
            dx = 0f;
            dy = 0f;
            var here = _field.FlowAt(g.X, g.Y, g.Flow);
            if (float.IsPositiveInfinity(here))
                return false;
            var look = Math.Max(_field.CellSize * 2f, g.R);
            var best = here;
            var bestAlign = -2f;
            var hx = (float)Math.Cos(g.Heading);
            var hy = (float)Math.Sin(g.Heading);
            for (var k = 0; k < LookDirections; k++)
            {
                var a = k * (float)(Math.PI * 2 / LookDirections);
                var ux = (float)Math.Cos(a);
                var uy = (float)Math.Sin(a);
                var c = _field.FlowAt(g.X + ux * look, g.Y + uy * look, g.Flow);
                var align = ux * hx + uy * hy;
                // Ties go to the direction closest to the current heading, so paths stay smooth.
                if (c < best - 1e-4f || (Math.Abs(c - best) <= 1e-4f && align > bestAlign))
                {
                    best = c;
                    bestAlign = align;
                    dx = ux;
                    dy = uy;
                }
            }

            return dx != 0f || dy != 0f;
        }

        /// <summary>How deep inside a target's centre must stay: half its radius, eased off for skinny silhouettes.</summary>
        float WalkDepth(float r) => Math.Min(r * 0.5f, _field.MaxDepth * 0.5f);

        /// <summary>Picks a far-away destination anywhere in the body and builds the inside-only route to it.</summary>
        void NewWaypoint(ref Target g)
        {
            if (g.Flow == null)
                g.Flow = new float[_field.CellCount];
            var walk = WalkDepth(g.R);
            var far = _field.Span * 0.3f;
            for (var attempt = 0; attempt < 12; attempt++)
            {
                // Destinations sit in comfortable parts of the body; thin ones only if the shape has none.
                if (!_field.RandomWalkable(_rng, walk, out var x, out var y)
                    && !_field.RandomWalkable(_rng, _field.InsideDepth, out x, out y))
                    break;
                var dx = x - g.X;
                var dy = y - g.Y;
                if (attempt < 8 && dx * dx + dy * dy < far * far)
                    continue;
                if (!_field.BuildFlow(x, y, walk, g.Flow))
                    continue;
                g.WayX = x;
                g.WayY = y;
                g.BestCost = _field.FlowAt(g.X, g.Y, g.Flow);
                g.Stuck = 0f;
                return;
            }

            g.BestCost = float.PositiveInfinity;
            g.Stuck = 0f;
        }

        void IntegrateFillers(float dt)
        {
            var k = _t.Spring;
            var c = _t.Damping;
            for (var i = 0; i < _f.Length; i++)
            {
                ref var f = ref _f[i];
                var ax = -k * (f.X - f.HomeX) - c * f.Vx;
                var ay = -k * (f.Y - f.HomeY) - c * f.Vy;
                f.Vx += ax * dt;
                f.Vy += ay * dt;
                f.X += f.Vx * dt;
                f.Y += f.Vy * dt;
                f.PreX = f.X;
                f.PreY = f.Y;
                f.Push = 0f;
            }
        }

        void PushFromTargets(float stiffness)
        {
            for (var gi = 0; gi < _g.Length; gi++)
            {
                ref var g = ref _g[gi];
                if (!g.Alive)
                    continue;
                var hx = (float)Math.Cos(g.Heading);
                var hy = (float)Math.Sin(g.Heading);
                var px = -hy;
                var py = hx;
                var reach = (g.R + _maxFillerR) * _t.PushReach;
                CellRange(g.X - reach, g.Y - reach, out var cx0, out var cy0);
                CellRange(g.X + reach, g.Y + reach, out var cx1, out var cy1);
                for (var cy = cy0; cy <= cy1; cy++)
                {
                    for (var cx = cx0; cx <= cx1; cx++)
                    {
                        for (var i = _head[cy * _gridW + cx]; i >= 0; i = _next[i])
                        {
                            ref var f = ref _f[i];
                            var contact = (g.R + f.R) * _t.PushReach;
                            var dx = f.X - g.X;
                            var dy = f.Y - g.Y;
                            var d2 = dx * dx + dy * dy;
                            if (d2 >= contact * contact)
                                continue;
                            var d = (float)Math.Sqrt(d2);
                            float nx, ny;
                            if (d > 1e-5f)
                            {
                                nx = dx / d;
                                ny = dy / d;
                            }
                            else
                            {
                                nx = (i & 1) == 0 ? px : -px;
                                ny = (i & 1) == 0 ? py : -py;
                            }

                            // Tiles ahead are shoved to the side the target is passing them on.
                            var ahead = nx * hx + ny * hy;
                            if (ahead > 0f)
                            {
                                var side = nx * px + ny * py >= 0f ? 1f : -1f;
                                var bias = _t.SidewaysBias * ahead;
                                nx = nx * (1f - bias) + px * side * bias;
                                ny = ny * (1f - bias) + py * side * bias;
                                var len = (float)Math.Sqrt(nx * nx + ny * ny);
                                nx /= len;
                                ny /= len;
                            }

                            var overlap = contact - d;
                            var move = overlap * stiffness;
                            f.X += nx * move;
                            f.Y += ny * move;
                            f.Push += move;
                            f.Heat = 1f;
                            g.Pressure += overlap * stiffness;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Shoved tiles push their neighbours so the shove ripples outward. Only "hot" tiles (recently
        /// shoved, directly or via the ripple) take part; as heat fades the springs alone bring every tile
        /// home, so tiles squeezed past each other can never stay jammed.
        /// </summary>
        void Relax(float dt)
        {
            const float coldHeat = 0.02f;
            var strength = _t.RelaxStrength * 0.5f;
            var cool = dt / HeatFadeSeconds;
            for (var i = 0; i < _f.Length; i++)
            {
                ref var a = ref _f[i];
                a.Heat = Math.Max(0f, a.Heat - cool);
            }

            for (var i = 0; i < _f.Length; i++)
            {
                ref var a = ref _f[i];
                var aCold = a.Heat < coldHeat;
                CellOf(a.X, a.Y, out var acx, out var acy);
                for (var cy = Math.Max(0, acy - 1); cy <= Math.Min(_gridH - 1, acy + 1); cy++)
                {
                    for (var cx = Math.Max(0, acx - 1); cx <= Math.Min(_gridW - 1, acx + 1); cx++)
                    {
                        for (var j = _head[cy * _gridW + cx]; j >= 0; j = _next[j])
                        {
                            if (j <= i)
                                continue;
                            ref var b = ref _f[j];
                            if (aCold && b.Heat < coldHeat)
                                continue;
                            var hx = a.HomeX - b.HomeX;
                            var hy = a.HomeY - b.HomeY;
                            var homeD = (float)Math.Sqrt(hx * hx + hy * hy);
                            // Only resolve overlap beyond what the packed layout already has.
                            var rest = Math.Min(a.R + b.R, homeD) * 0.97f;
                            var dx = b.X - a.X;
                            var dy = b.Y - a.Y;
                            var d2 = dx * dx + dy * dy;
                            if (d2 >= rest * rest)
                                continue;
                            var d = (float)Math.Sqrt(d2);
                            if (d < 1e-5f)
                            {
                                dx = hx == 0f && hy == 0f ? 1f : -hx;
                                dy = hx == 0f && hy == 0f ? 0f : -hy;
                                d = Math.Max(1e-5f, (float)Math.Sqrt(dx * dx + dy * dy));
                            }

                            var heat = Math.Max(a.Heat, b.Heat);
                            var corr = (rest - d) * strength * heat;
                            var ux = dx / d * corr;
                            var uy = dy / d * corr;
                            a.X -= ux;
                            a.Y -= uy;
                            b.X += ux;
                            b.Y += uy;
                            a.Push += corr;
                            b.Push += corr;
                            var spread = heat * HeatSpread;
                            if (a.Heat < spread) a.Heat = spread;
                            if (b.Heat < spread) b.Heat = spread;
                        }
                    }
                }
            }
        }

        void ClampOffsets()
        {
            for (var i = 0; i < _f.Length; i++)
            {
                ref var f = ref _f[i];
                var ox = f.X - f.HomeX;
                var oy = f.Y - f.HomeY;
                var off = (float)Math.Sqrt(ox * ox + oy * oy);
                // Room for a full target to sit on the home slot, else the clamp would bury tiles under it.
                var maxOff = Math.Max(f.R * _t.MaxOffset, (_maxTargetR + f.R) * _t.PushReach * 1.25f);
                if (off <= maxOff)
                    continue;
                var s = maxOff / off;
                f.X = f.HomeX + ox * s;
                f.Y = f.HomeY + oy * s;
            }
        }

        void SettleFillers(float dt)
        {
            var invDt = 1f / dt;
            var turnBlend = Clamp01(dt * 10f);
            var squashBlend = Clamp01(dt * 12f);
            for (var i = 0; i < _f.Length; i++)
            {
                ref var f = ref _f[i];
                f.Vx += (f.X - f.PreX) * invDt * _t.Carry;
                f.Vy += (f.Y - f.PreY) * invDt * _t.Carry;

                var ox = f.X - f.HomeX;
                var oy = f.Y - f.HomeY;
                var off = (float)Math.Sqrt(ox * ox + oy * oy);

                // Twist with the swirl of motion around home: shoved tiles rotate, then untwist as they spring back.
                var swirl = off > 1e-5f ? (ox * f.Vy - oy * f.Vx) / off : 0f;
                var angle = Clamp(swirl / _tile * _t.Wobble, -28f, 28f);
                f.Angle += (angle - f.Angle) * turnBlend;
                var squash = Clamp01(f.Push / Math.Max(1e-5f, f.R * 0.15f));
                f.Squash += (squash - f.Squash) * squashBlend;
            }
        }

        void SeparateTargets()
        {
            for (var i = 0; i < _g.Length; i++)
            {
                if (!_g[i].Alive)
                    continue;
                for (var j = i + 1; j < _g.Length; j++)
                {
                    if (!_g[j].Alive)
                        continue;
                    var dx = _g[j].X - _g[i].X;
                    var dy = _g[j].Y - _g[i].Y;
                    var rest = _g[i].R + _g[j].R;
                    var d2 = dx * dx + dy * dy;
                    if (d2 >= rest * rest || d2 < 1e-10f)
                        continue;
                    var d = (float)Math.Sqrt(d2);
                    var corr = (rest - d) * 0.5f / d;
                    NudgeInside(ref _g[i], -dx * corr, -dy * corr);
                    NudgeInside(ref _g[j], dx * corr, dy * corr);
                }
            }
        }

        /// <summary>Moves a target unless that would carry its centre out of the silhouette.</summary>
        void NudgeInside(ref Target g, float dx, float dy)
        {
            var nx = g.X + dx;
            var ny = g.Y + dy;
            if (_field.Depth(nx, ny) < _field.InsideDepth && _field.Depth(nx, ny) < _field.Depth(g.X, g.Y))
                return;
            g.X = nx;
            g.Y = ny;
        }

        void RebuildGrid()
        {
            for (var i = 0; i < _head.Length; i++)
                _head[i] = -1;
            for (var i = 0; i < _f.Length; i++)
            {
                CellOf(_f[i].X, _f[i].Y, out var cx, out var cy);
                var c = cy * _gridW + cx;
                _next[i] = _head[c];
                _head[c] = i;
            }
        }

        void CellOf(float x, float y, out int cx, out int cy) => CellRange(x, y, out cx, out cy);

        void CellRange(float x, float y, out int cx, out int cy)
        {
            cx = (int)((x - _gridMinX) / _gridCell);
            cy = (int)((y - _gridMinY) / _gridCell);
            cx = cx < 0 ? 0 : (cx >= _gridW ? _gridW - 1 : cx);
            cy = cy < 0 ? 0 : (cy >= _gridH ? _gridH - 1 : cy);
        }

        static float WrapAngle(float a)
        {
            const float twoPi = (float)(Math.PI * 2);
            while (a > Math.PI) a -= twoPi;
            while (a < -Math.PI) a += twoPi;
            return a;
        }

        static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
        static float Clamp01(float v) => Clamp(v, 0f, 1f);
    }
}
