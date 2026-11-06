using System;

namespace Game.Core
{
    [Serializable]
    public sealed class DriftLockTuning
    {
        /// <summary>Seconds on target before the lock engages.</summary>
        public float LockSeconds = 1f;
        /// <summary>Spatial error limit while acquiring: target distance from lens centre / lens radius.</summary>
        public float AimTolerance = 0.45f;
        /// <summary>Looser spatial limit once locked (hysteresis), same units as AimTolerance.</summary>
        public float LockTolerance = 0.6f;
        /// <summary>Temporal error limit: total off-target time allowed while acquiring, as a share of LockSeconds.</summary>
        public float OffTargetAllowance = 0.2f;
        /// <summary>One continuous slip longer than this breaks the acquire.</summary>
        public float AcquireDropout = 0.25f;
        /// <summary>Seconds out of LockTolerance before a lock is lost; lifting after that does nothing.</summary>
        public float LockLostAfter = 0.2f;
    }

    public enum DriftLockState
    {
        Idle,
        Acquiring,
        Locked
    }

    public enum DriftLockEvent
    {
        None,
        /// <summary>Started acquiring FocusId.</summary>
        Acquiring,
        /// <summary>Acquire failed (slipped off) before the lock engaged.</summary>
        Broken,
        Locked,
        /// <summary>A lock was held but the target escaped.</summary>
        Lost,
        /// <summary>Finger lifted while locked: FiredId is shot down.</summary>
        Fired,
        /// <summary>Finger lifted before a lock: nothing happens.</summary>
        Released
    }

    /// <summary>
    /// Fighter-jet style lock-on: keep the lens on a candidate for LockSeconds (within the spatial and
    /// temporal error limits) to lock it, then fire while it is still tracked. The host decides what
    /// "tracking" and "fire" mean per input device: mouse = hovering / click, touch = finger down / finger lift.
    /// </summary>
    public sealed class DriftLockTracker
    {
        public const int None = -1;

        readonly DriftLockTuning _tuning;
        float _onTime;
        float _offTotal;
        float _offRun;

        public DriftLockTracker(DriftLockTuning tuning)
        {
            _tuning = tuning ?? new DriftLockTuning();
        }

        public DriftLockState State { get; private set; }
        public int FocusId { get; private set; } = None;
        public int FiredId { get; private set; } = None;
        public float Progress01 => State == DriftLockState.Locked ? 1f : Clamp01(_onTime / Math.Max(0.01f, _tuning.LockSeconds));

        /// <param name="active">The lens is currently allowed to track (hovering, or finger down).</param>
        /// <param name="fire">Fire pressed this frame (click, or finger lifted). Evaluated before <paramref name="active"/>.</param>
        /// <param name="nearestId">Candidate closest to the lens centre, or None.</param>
        /// <param name="errorOf">Distance of a candidate from the lens centre / lens radius; infinity when gone.</param>
        public DriftLockEvent Tick(float dt, bool active, bool fire, int nearestId, Func<int, float> errorOf)
        {
            FiredId = None;

            if (fire)
            {
                if (State == DriftLockState.Idle)
                    return DriftLockEvent.None;
                if (State == DriftLockState.Locked)
                {
                    FiredId = FocusId;
                    Reset();
                    return DriftLockEvent.Fired;
                }

                Reset();
                return DriftLockEvent.Released;
            }

            if (!active)
            {
                // Pointer left the board (or the HUD took it): any acquire or lock is dropped.
                var was = State;
                Reset();
                return was == DriftLockState.Locked ? DriftLockEvent.Lost
                    : was == DriftLockState.Acquiring ? DriftLockEvent.Broken
                    : DriftLockEvent.None;
            }

            switch (State)
            {
                case DriftLockState.Idle:
                    if (nearestId == None || errorOf(nearestId) > _tuning.AimTolerance)
                        return DriftLockEvent.None;
                    Reset();
                    FocusId = nearestId;
                    State = DriftLockState.Acquiring;
                    _onTime = dt;
                    return DriftLockEvent.Acquiring;

                case DriftLockState.Acquiring:
                    if (errorOf(FocusId) <= _tuning.AimTolerance)
                    {
                        _onTime += dt;
                        _offRun = 0f;
                    }
                    else
                    {
                        _offTotal += dt;
                        _offRun += dt;
                        if (_offRun > _tuning.AcquireDropout || _offTotal > _tuning.OffTargetAllowance * _tuning.LockSeconds)
                        {
                            Reset();
                            return DriftLockEvent.Broken;
                        }
                    }

                    if (_onTime < _tuning.LockSeconds - 1e-4f)
                        return DriftLockEvent.None;
                    State = DriftLockState.Locked;
                    _offRun = 0f;
                    return DriftLockEvent.Locked;

                case DriftLockState.Locked:
                    if (errorOf(FocusId) <= _tuning.LockTolerance)
                    {
                        _offRun = 0f;
                        return DriftLockEvent.None;
                    }

                    _offRun += dt;
                    if (_offRun <= _tuning.LockLostAfter)
                        return DriftLockEvent.None;
                    Reset();
                    return DriftLockEvent.Lost;
            }

            return DriftLockEvent.None;
        }

        /// <summary>Drops any acquire/lock without an event (level end, pause).</summary>
        public void Reset()
        {
            State = DriftLockState.Idle;
            FocusId = None;
            _onTime = 0f;
            _offTotal = 0f;
            _offRun = 0f;
        }

        static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
