using System;
using Game.Core;
using NUnit.Framework;

namespace Game.Core.Tests
{
    [TestFixture]
    public class DriftLockTrackerTests
    {
        const float Dt = 0.05f;
        const int A = 0;
        const int B = 1;

        float _errA;
        float _errB;
        DriftLockTracker _tracker;
        Func<int, float> _errorOf;

        [SetUp]
        public void SetUp()
        {
            _wasHeld = false;
            _errA = 0f;
            _errB = float.PositiveInfinity;
            _tracker = new DriftLockTracker(new DriftLockTuning());
            _errorOf = id => id == A ? _errA : id == B ? _errB : float.PositiveInfinity;
        }

        bool _wasHeld;

        /// <summary>Touch-style input: tracking while the finger is down, fire on the frame it lifts.</summary>
        DriftLockEvent Tick(bool held = true, int nearest = A)
        {
            var lifted = _wasHeld && !held;
            _wasHeld = held;
            return _tracker.Tick(Dt, held, lifted, nearest, _errorOf);
        }

        /// <summary>Mouse-style input: tracking while hovering, fire on click.</summary>
        DriftLockEvent Hover(bool click = false, bool onBoard = true, int nearest = A) =>
            _tracker.Tick(Dt, onBoard, click, nearest, _errorOf);

        DriftLockEvent HoldFor(float seconds, int nearest = A)
        {
            var last = DriftLockEvent.None;
            for (var t = 0f; t < seconds - 1e-4f; t += Dt)
            {
                var e = Tick(true, nearest);
                if (e != DriftLockEvent.None)
                    last = e;
            }

            return last;
        }

        [Test]
        public void LocksAfterLockSecondsOnTarget()
        {
            Assert.That(Tick(), Is.EqualTo(DriftLockEvent.Acquiring));
            Assert.That(_tracker.State, Is.EqualTo(DriftLockState.Acquiring));
            Assert.That(HoldFor(0.9f), Is.EqualTo(DriftLockEvent.None));
            Assert.That(Tick(), Is.EqualTo(DriftLockEvent.Locked));
            Assert.That(_tracker.State, Is.EqualTo(DriftLockState.Locked));
            Assert.That(_tracker.Progress01, Is.EqualTo(1f));
        }

        [Test]
        public void NoAcquireOutsideAimTolerance()
        {
            _errA = 0.8f;
            Assert.That(Tick(), Is.EqualTo(DriftLockEvent.None));
            Assert.That(_tracker.State, Is.EqualTo(DriftLockState.Idle));
        }

        [Test]
        public void BriefJitterWithinAllowanceStillLocks()
        {
            Tick();
            HoldFor(0.4f);
            _errA = 0.6f;
            HoldFor(0.1f);
            _errA = 0.1f;
            Assert.That(HoldFor(0.6f), Is.EqualTo(DriftLockEvent.Locked));
        }

        [Test]
        public void LongDropoutBreaksAcquire()
        {
            Tick();
            HoldFor(0.3f);
            _errA = 0.9f;
            Assert.That(HoldFor(0.3f), Is.EqualTo(DriftLockEvent.Broken));
            Assert.That(_tracker.State, Is.EqualTo(DriftLockState.Idle));
            Assert.That(_tracker.Progress01, Is.EqualTo(0f));
        }

        [Test]
        public void TotalOffTargetBudgetBreaksAcquire()
        {
            Tick();
            // Each slip is short (under AcquireDropout) but together they exceed 20% of LockSeconds.
            var broken = false;
            for (var i = 0; i < 6 && !broken; i++)
            {
                _errA = 0.1f;
                HoldFor(0.1f);
                _errA = 0.9f;
                broken = HoldFor(0.1f) == DriftLockEvent.Broken;
            }

            Assert.That(broken, Is.True);
        }

        [Test]
        public void ReleaseWhileLockedFires()
        {
            Tick();
            HoldFor(1f);
            Assert.That(Tick(false), Is.EqualTo(DriftLockEvent.Fired));
            Assert.That(_tracker.FiredId, Is.EqualTo(A));
            Assert.That(_tracker.State, Is.EqualTo(DriftLockState.Idle));
        }

        [Test]
        public void ReleaseShortlyAfterSlipStillFires()
        {
            Tick();
            HoldFor(1f);
            _errA = 0.9f;
            Tick();
            Assert.That(Tick(false), Is.EqualTo(DriftLockEvent.Fired));
        }

        [Test]
        public void TargetEscapingLosesLockAndReleaseDoesNothing()
        {
            Tick();
            HoldFor(1f);
            _errA = 0.9f;
            Assert.That(HoldFor(0.3f), Is.EqualTo(DriftLockEvent.Lost));
            Assert.That(Tick(false), Is.EqualTo(DriftLockEvent.None));
            Assert.That(_tracker.FiredId, Is.EqualTo(DriftLockTracker.None));
        }

        [Test]
        public void LockHoldsWithinLooserLockTolerance()
        {
            Tick();
            HoldFor(1f);
            _errA = 0.55f;
            Assert.That(HoldFor(1f), Is.EqualTo(DriftLockEvent.None));
            Assert.That(_tracker.State, Is.EqualTo(DriftLockState.Locked));
        }

        [Test]
        public void ReleaseBeforeLockIsHarmless()
        {
            Tick();
            HoldFor(0.4f);
            Assert.That(Tick(false), Is.EqualTo(DriftLockEvent.Released));
            Assert.That(_tracker.FiredId, Is.EqualTo(DriftLockTracker.None));
        }

        [Test]
        public void TargetRemovedMidAcquireBreaks()
        {
            Tick();
            HoldFor(0.3f);
            _errA = float.PositiveInfinity;
            Assert.That(HoldFor(0.3f), Is.EqualTo(DriftLockEvent.Broken));
        }

        [Test]
        public void SwitchingTargetsRestartsProgress()
        {
            Tick();
            HoldFor(0.5f);
            _errA = 0.9f;
            _errB = 0f;
            var broken = false;
            for (var i = 0; i < 6 && !broken; i++)
                broken = Tick(true, B) == DriftLockEvent.Broken;
            Assert.That(broken, Is.True);
            Assert.That(Tick(true, B), Is.EqualTo(DriftLockEvent.Acquiring));
            Assert.That(_tracker.FocusId, Is.EqualTo(B));
            Assert.That(_tracker.Progress01, Is.LessThan(0.1f));
        }

        [Test]
        public void HoverLocksWithoutAnyPress()
        {
            Assert.That(Hover(), Is.EqualTo(DriftLockEvent.Acquiring));
            DriftLockEvent last = DriftLockEvent.None;
            for (var i = 0; i < 20; i++)
            {
                var e = Hover();
                if (e != DriftLockEvent.None)
                    last = e;
            }

            Assert.That(last, Is.EqualTo(DriftLockEvent.Locked));
        }

        [Test]
        public void ClickWhileLockedFires()
        {
            Hover();
            for (var i = 0; i < 20; i++)
                Hover();
            Assert.That(_tracker.State, Is.EqualTo(DriftLockState.Locked));
            Assert.That(Hover(click: true), Is.EqualTo(DriftLockEvent.Fired));
            Assert.That(_tracker.FiredId, Is.EqualTo(A));
        }

        [Test]
        public void ClickBeforeLockOrOnNothingIsHarmless()
        {
            _errA = 0.9f;
            Assert.That(Hover(click: true), Is.EqualTo(DriftLockEvent.None));
            _errA = 0f;
            Hover();
            Hover();
            Assert.That(Hover(click: true), Is.EqualTo(DriftLockEvent.Released));
            Assert.That(_tracker.FiredId, Is.EqualTo(DriftLockTracker.None));
        }

        [Test]
        public void ClickAfterTargetEscapedDoesNothing()
        {
            Hover();
            for (var i = 0; i < 20; i++)
                Hover();
            _errA = 0.9f;
            var lost = false;
            for (var i = 0; i < 8 && !lost; i++)
                lost = Hover() == DriftLockEvent.Lost;
            Assert.That(lost, Is.True);
            Assert.That(Hover(click: true), Is.EqualTo(DriftLockEvent.None));
            Assert.That(_tracker.FiredId, Is.EqualTo(DriftLockTracker.None));
        }

        [Test]
        public void LeavingTheBoardDropsTheLock()
        {
            Hover();
            for (var i = 0; i < 20; i++)
                Hover();
            Assert.That(Hover(onBoard: false), Is.EqualTo(DriftLockEvent.Lost));
            Assert.That(_tracker.State, Is.EqualTo(DriftLockState.Idle));
        }
    }
}
